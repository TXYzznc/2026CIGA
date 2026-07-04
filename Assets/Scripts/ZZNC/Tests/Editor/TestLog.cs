using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

internal static class TestLog
{
    public static void Start(string testName, string purpose)
    {
        Write("========== " + testName + " ==========");
        Write("目的: " + purpose);
    }

    public static void Step(string message)
    {
        Write("[步骤] " + message);
    }

    public static void State(string label, object value)
    {
        Write("[状态] " + label + ": " + Format(value));
    }

    public static void Expect(string message)
    {
        Write("[期望] " + message);
    }

    public static void Actual(string label, object value)
    {
        Write("[实际] " + label + ": " + Format(value));
    }

    public static void Pass(string message)
    {
        Write("[通过] " + message);
    }

    public static void Board(Board board)
    {
        Write("[棋盘] 棋子:");
        foreach (var piece in board.AllPieces())
            Write("  - " + Piece(piece));

        Write("[棋盘] 空格数: " + board.EmptyCells().Count);
    }

    public static string Piece(Piece piece)
    {
        if (piece == null) return "null";
        return "#" + piece.ID + " " + piece.Type + " @ " + piece.Position;
    }

    public static string Event(GameEvent ev)
    {
        if (ev == null) return "null";
        return ev.Type + " target=" + ev.TargetPieceId + " source=" + ev.SourcePieceId + " dir=D" + ev.Direction;
    }

    public static string HexList(IEnumerable<Hex> cells)
    {
        var values = new List<string>();
        foreach (var cell in cells)
            values.Add(cell.ToString());
        return string.Join(", ", values);
    }

    private static string Format(object value)
    {
        return value == null ? "null" : value.ToString();
    }

    private static void Write(string message)
    {
        TestContext.WriteLine(message);
        Debug.Log("[ZZNC.Tests] " + message);
    }
}
