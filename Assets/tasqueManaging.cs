﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using rnd = UnityEngine.Random;

public class tasqueManaging : MonoBehaviour
{
    public new KMAudio audio;
    public KMBombInfo bomb;
    public KMBombModule module;

    public KMSelectable[] tiles;
    private Renderer[] tileRenders;
    public KMSelectable submitButton;
    public Renderer[] leds;
    public TextMesh screenText;
    public Material litMat;
    private Material blackMat;
    public Color[] tileColors;

    private int startingPosition;
    private int currentPosition;
    private int[] movableTiles;
    private int[] goalTiles = new int[3];
    private int stage;
    private string[] maze;

    private static float waitTime = 15f;
    private static readonly int[][] groupIndices = new int[][]
    {
        new int[] { 0, 2, 4, 1 },
        new int[] { 3, 7, 10, 6 },
        new int[] { 5, 9, 12, 8 },
        new int[] { 11, 14, 15, 13 }
    };
    private static readonly int[][] adjacentTiles = new int[][]
     {
        new int[] { -1, -1, 1, 2 },
        new int[] { -1, 0, 3, 4 },
        new int[] { 0, -1, 4, 5 },
        new int[] { -1, 1, 6, 7 },
        new int[] { 1, 2, 7, 8 },
        new int[] { 2, -1, 8, 9 },
        new int[] { -1, 3, -1, 10 },
        new int[] { 3, 4, 10, 11 },
        new int[] { 4, 5, 11, 12 },
        new int[] { 5, -1, 12, -1 },
        new int[] { 6, 7, -1, 13 },
        new int[] { 7, 8, 13, 14 },
        new int[] { 8, 9, 14, -1 },
        new int[] { 10, 11, -1, 15 },
        new int[] { 11, 12, 15, -1 },
        new int[] { 13, 14, -1, -1 }
    };
    private static readonly string[] mazes = new string[]
    {
        "2;123;23;12;01;03;13;2;23;02;01;13;01;3;02;01",
        "23;13;03;2;0;023;13;23;13;0;01;02;02;13;12;01",
        "23;13;0;23;02;23;13;01;1;02;03;23;12;01;012;1",
        "23;12;02;13;13;23;3;02;01;02;013;3;1;03;02;01"
    };

    private Coroutine countUp;
    private bool bombStarted;
    private bool animating;
    private bool moduleActive;
#pragma warning disable 0649
    private bool TwitchPlaysActive;
#pragma warning restore 0649

    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved;

    private void Awake()
    {
        moduleId = moduleIdCounter++;
        module.OnActivate += delegate () { bombStarted = true; if (TwitchPlaysActive) { waitTime = 30f; } };
        tileRenders = tiles.Select(x => x.GetComponent<Renderer>()).ToArray();
        blackMat = leds[0].material;
        foreach (KMSelectable tile in tiles)
        {
            var ix = Array.IndexOf(tiles, tile);
            tile.OnInteract += delegate () { PressTile(tile); return false; };
            tile.OnHighlight += delegate ()
            {
                if (moduleSolved || !bombStarted || animating)
                    return;
                else if (ix == currentPosition)
                    tileRenders[ix].material.color = tileColors[3];
                else if (movableTiles.Contains(ix))
                    tileRenders[ix].material.color = tileColors[2];
            };
            tile.OnHighlightEnded += delegate ()
            {
                if (moduleSolved || !bombStarted || animating)
                    return;
                else if (ix == currentPosition)
                    tileRenders[ix].material.color = tileColors[1];
                else if (movableTiles.Contains(ix))
                    tileRenders[ix].material.color = tileColors[0];
            };
        }
        submitButton.OnInteract += delegate () { PressSubmitButton(); return false; };
    }

    private void Start()
    {
        maze = mazes[bomb.GetSerialNumberNumbers().First() % 4].Split(';').ToArray();
        startingPosition = rnd.Range(0, 16);
        currentPosition = startingPosition;
        Debug.LogFormat("[Tasque Managing #{0}] We begin at {1}, {1}!", moduleId, PositionName(startingPosition));
        tileRenders[startingPosition].material.color = tileColors[1];
        movableTiles = adjacentTiles[startingPosition].ToArray();
        do
        {
            for (int i = 0; i < 3; i++)
                goalTiles[i] = rnd.Range(0, 16);
        }
        while (goalTiles.Any(x => x == startingPosition) || goalTiles.Distinct().Count() != 3);
    }

    private void PressTile(KMSelectable tile)
    {
        tile.AddInteractionPunch(.1f);
        var ix = Array.IndexOf(tiles, tile);
        if (moduleSolved || !bombStarted || animating)
            return;
        if (!moduleActive)
        {
            if (ix != startingPosition)
                return;
            moduleActive = true;
            Debug.LogFormat("[Tasque Managing #{0}] Module activated, module activated!", moduleId);
            Debug.LogFormat("[Tasque Managing #{0}] Tiles to visit: {1}!", moduleId, goalTiles.Select(x => PositionName(x)).Join(", "));
            countUp = StartCoroutine(CountUp());
        }
        else
        {
            if (!movableTiles.Contains(ix))
                return;
            if (!maze[currentPosition].Contains(Array.IndexOf(movableTiles, ix).ToString()))
            {
                Debug.LogFormat("[Tasque Managing #{0}] No, no! You ran into a wall!", moduleId);
                StartCoroutine(Strike());
            }
            else
            {
                tileRenders[currentPosition].material.color = tileColors[0];
                currentPosition = ix;
                movableTiles = adjacentTiles[ix].ToArray();
                if (!TwitchPlaysActive)
                    tileRenders[ix].material.color = tileColors[3];
                else
                    tileRenders[ix].material.color = tileColors[2];
            }
        }
    }

    private void PressSubmitButton()
    {
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, submitButton.transform);
        submitButton.AddInteractionPunch(.25f);
        if (moduleSolved || animating || !bombStarted || !moduleActive)
            return;
        if (currentPosition == goalTiles[stage])
        {
            Debug.LogFormat("[Tasque Managing #{0}] You've made it to {1}!", moduleId, PositionName(currentPosition));
            leds[stage].material = litMat;
            stage++;
            if (stage == 3)
            {
                moduleSolved = true;
                module.HandlePass();
                audio.PlaySoundAtTransform("solve", transform);
                Debug.LogFormat("[Tasque Managing #{0}] The module is solved, that it is!", moduleId);
                tileRenders[currentPosition].material.color = tileColors[0];
                if (countUp != null)
                {
                    StopCoroutine(countUp);
                    countUp = null;
                }
                StartCoroutine(SolveAnimation());
            }
            else
            {
                if (countUp != null)
                {
                    StopCoroutine(countUp);
                    countUp = null;
                }
                countUp = StartCoroutine(CountUp());
            }
        }
        else
            StartCoroutine(Strike());
    }

    private IEnumerator CountUp()
    {
        animating = true;
        var group = 0;
        for (int i = 0; i < 4; i++)
            if (groupIndices[i].Contains(goalTiles[stage]))
                group = i;
        var subtile = Array.IndexOf(groupIndices[group], goalTiles[stage]);
        var groupFirst = bomb.GetSerialNumberNumbers().Last() % 2 == 0;
        audio.PlaySoundAtTransform("ABCD"[groupFirst ? group : subtile].ToString(), transform);
        StartCoroutine(ShowLetter("ABCD"[groupFirst ? group : subtile]));
        yield return new WaitForSeconds(.5f);
        audio.PlaySoundAtTransform("ABCD"[groupFirst ? subtile : group].ToString(), transform);
        StartCoroutine(ShowLetter("ABCD"[groupFirst ? subtile : group]));
        Debug.LogFormat("[Tasque Managing #{0}] I have said {1}{2}, I have I have!", moduleId, "ABCD"[groupFirst ? group : subtile], "ABCD"[groupFirst ? subtile : group].ToString());
        yield return new WaitForSeconds(.5f);
        animating = false;
        yield return new WaitForSeconds(waitTime);
        StartCoroutine(Strike());
    }

    private IEnumerator ShowLetter(char letter)
    {
        screenText.color = Color.white;
        screenText.text = letter.ToString();
        var elapsed = 0f;
        var duration = .49f;
        while (elapsed < duration)
        {
            screenText.color = Color.Lerp(Color.white, Color.clear, elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        screenText.color = Color.clear;
    }

    private IEnumerator Strike()
    {
        animating = true;
        if (countUp != null)
        {
            StopCoroutine(countUp);
            countUp = null;
        }
        Debug.LogFormat("[Tasque Managing #{0}] You ended up at {1}! No, no!", moduleId, PositionName(currentPosition));
        Debug.LogFormat("[Tasque Managing #{0}] Someone ought to whip you into shape! Back to the beginning you go!", moduleId);
        module.HandleStrike();
        audio.PlaySoundAtTransform("strike", transform);
        foreach (Renderer tile in tileRenders)
            tile.material.color = tileColors[4];
        yield return new WaitForSeconds(2f);
        stage = 0;
        foreach (Renderer tile in tileRenders)
            tile.material.color = tileColors[0];
        foreach (Renderer led in leds)
            led.material = blackMat;
        animating = false;
        moduleActive = false;
        Start();
    }

    private IEnumerator SolveAnimation()
    {
        var order = new int[] { 0, 2, 5, 9, 12, 14, 15, 13, 10, 6, 3, 1, 4, 8, 11, 7 };
        for (int i = 0; i < 16; i++)
        {
            tileRenders[order[i]].material.color = tileColors[5];
            yield return new WaitForSeconds(.1f);
        }
    }

    private static string PositionName(int ix)
    {
        var directions = new string[] { "up", "left", "right", "down" };
        var directionsButBlah = new string[] { "up", "right", "down", "left" };
        var part1 = 0;
        for (int i = 0; i < 4; i++)
            if (groupIndices[i].Contains(ix))
                part1 = i;
        var part2 = Array.IndexOf(groupIndices[part1], ix);
        return directions[part1] + "-" + directionsButBlah[part2];
    }

    // Twitch Plays
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "!{0} activate [Begins the module. On Twitch Plays, you have 30 seconds to move instead of 15.] !{0} submit [Presses the submit button.] !{0} <TL/TR/BL/BR> [Moves in that direction, can be chained with spaces, e.g. !{0} TL BL TR]";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string input)
    {
        yield return "strike";
        yield return "solve";
        var directions = new string[] { "TL", "TR", "BL", "BR" };
        input = input.Trim().ToUpperInvariant();
        if (input == "ACTIVATE" || input == "START" || input == "BEGIN" || input == "GO")
        {
            yield return null;
            tiles[startingPosition].OnInteract();
        }
        else if (input == "SUBMIT")
        {
            yield return null;
            submitButton.OnInteract();
        }
        else if (input.Split(' ').All(x => directions.Contains(x)))
        {
            yield return null;
            foreach (string str in input.Split(' '))
            {
                var ix = Array.IndexOf(directions, str);
                if (movableTiles[ix] == -1)
                    yield break;
                else
                {
                    yield return new WaitForSeconds(.2f);
                    tiles[movableTiles[ix]].OnInteract();
                }
            }
        }
        else
            yield break;
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        while (!moduleSolved)
        {
            if (!moduleActive)
            {
                yield return null;
                tiles[startingPosition].OnInteract();
            }
            var q = new Queue<int>();
            var allMoves = new List<movement>();
            q.Enqueue(currentPosition);
            while (q.Count > 0)
            {
                var next = q.Dequeue();
                if (next == goalTiles[stage])
                    goto readyToSubmit;
                var cell = maze[next];
                for (int i = 0; i < 4; i++)
                {
                    if (cell.Contains(i.ToString()) && !allMoves.Any(x => x.start == adjacentTiles[next][i]))
                    {
                        q.Enqueue(adjacentTiles[next][i]);
                        allMoves.Add(new movement(next, adjacentTiles[next][i], i));
                    }
                }
            }
            throw new InvalidOperationException("There is a bug in maze generation.");
        readyToSubmit:
            while (animating)
                yield return true;
            if (allMoves.Count != 0) // Checks for position already being target
            {
                var lastMove = allMoves.First(x => x.end == goalTiles[stage]);
                var relevantMoves = new List<movement> { lastMove };
                while (lastMove.start != currentPosition)
                {
                    lastMove = allMoves.First(x => x.end == lastMove.start);
                    relevantMoves.Add(lastMove);
                }
                for (int i = 0; i < relevantMoves.Count; i++)
                {
                    var thisMove = relevantMoves[relevantMoves.Count - 1 - i];
                    tiles[adjacentTiles[thisMove.start][thisMove.direction]].OnInteract();
                    yield return new WaitForSeconds(.1f);
                }
            }
            yield return new WaitForSeconds(.1f);
            submitButton.OnInteract();
        }
    }

    private class movement
    {
        public int start { get; set; }
        public int end { get; set; }
        public int direction { get; set; }

        public movement(int s, int e, int d)
        {
            start = s;
            end = e;
            direction = d;
        }
    }
}
