using System;
using System.Collections.Generic;
using System.Linq;
using DTLib;
using DTLib.Extensions;

namespace launcher_client;

public class ConsoleWrapper : IConsoleWrapper
{
    private List<string> _textLines = new();
    private int _linesUp;
    private string _headerText = "";
    private string _footerText = "";

    private Timer _consoleSizeCheckTimer;

    public int TextAreaW;
    public int TextAreaH;
    
    
    public ConsoleWrapper()
    {
        int lastW = Console.WindowWidth;
        int lastH = Console.WindowHeight;
        _consoleSizeCheckTimer = new Timer(true, 50, () =>
        {
            if(Console.WindowWidth != lastW || Console.WindowHeight != lastH)
            {
                lastW = Console.WindowWidth;
                lastH = Console.WindowHeight;
                Console.Clear();
                DrawGui();
            }
        });
    }

    /// <summary>
    /// starts automatig gui redraw on console size change
    /// </summary>
    public void StartUpdating()
    {
        _consoleSizeCheckTimer.Start();
    }
    
    public void StopUpdating()
    {
        _consoleSizeCheckTimer.Stop();
    }
    
    public void WriteLine(string msg)
    {
        _textLines.Add(msg);
        DrawGui();
    }

    public void SetHeader(string s) => _headerText = s;
    
    public void SetFooter(string s) => _footerText = s;
    
    public void ScrollDown(int lines = 1)
    {
        _linesUp -= lines;
        if (_linesUp < 0)
            _linesUp = 0;
        DrawGui();
    }
    
    public void ScrollUp(int lines = 1)
    {
        _linesUp += lines;
        DrawGui();
    }
    
    public void DrawGui()
    {
        var b = new ConsoleBuffer();
        TextAreaW = b.Width - 6;
        TextAreaH = b.Height - 6;
        Console.ForegroundColor = ConsoleColor.White;
        Console.CursorVisible = false;
        DrawBorders(b);
        DrawText(b);
        DrawScrollBar(b);
        DrawHeader(b);
        DrawFooter(b);
        b.Print();
    }

    private void DrawBorders(ConsoleBuffer b)
    {
        b.Write(    '┏' + '━'.Multiply(b.Width - 2) +   '┓');
        b.Write(    '┃' + ' '.Multiply(b.Width - 2) +   '┃');
        b.Write(    '┣' + '━'.Multiply(b.Width - 4) + "┳━┫");
        for (int y = 0; y < b.Height - 6; y++)
            b.Write('┃' + ' '.Multiply(b.Width - 4) + "┃ ┃");
        b.Write(    '┣' + '━'.Multiply(b.Width - 4) + "┻━┫");
        b.Write(    '┃' + ' '.Multiply(b.Width - 2) +   '┃');
        b.Write(    '┗' + '━'.Multiply(b.Width - 2) +   '┛');
    }

    private void DrawScrollBar(ConsoleBuffer b)
    {
        int scrollBarX = b.Width - 2;
        int scrollBarY = 3;

        int slideH = 0;
        if (_textLines.Count >= TextAreaH)
            slideH = (int)Math.Ceiling((double)TextAreaH * TextAreaH / _textLines.Count);
        int slidePos = (int)Math.Ceiling((double) _linesUp / TextAreaH) + 1;
        for(int y = 0; y < slideH; y++)
        {
            b.SetCursorPosition(scrollBarX, scrollBarY + TextAreaH - y - slidePos);
            b.Write('▒');
        }
    }

    private void DrawHeader(ConsoleBuffer b)
    {
        b.SetCursorPosition(2, 1);
        b.Write(ChopLongLine(_headerText, b.Width - 4));
    }

    private void DrawFooter(ConsoleBuffer b)
    {
        b.SetCursorPosition(2, b.Height - 2);
        b.Write(ChopLongLine(_footerText, b.Width - 4));
    }

    private static string ChopLongLine(string s, int maxLength)
    {
        if (s.Length <= maxLength)
            return s;

        return s.Remove(maxLength - 3) + "...";
    }

    private void DrawText(ConsoleBuffer b)
    {
        int textAreaX = 2;
        int textAreaY = 3;
        
        var realLines = _textLines
            .SelectMany(s => SplitStringToLines(s, TextAreaW))
            .ToArray();

        int linesUp = _linesUp + TextAreaH;
        if (linesUp > realLines.Length)
        {
            linesUp = realLines.Length;
            _linesUp = Math.Max(0, realLines.Length - TextAreaH);
        }
        
        for (int y = 0; y < TextAreaH; y++)
        {
            b.SetCursorPosition(textAreaX, textAreaY + y);
            int li = realLines.Length - linesUp + y;
            if (li >= realLines.Length)
                break;
            b.Write(realLines[li]);
        }
    }

    private static ICollection<string> SplitStringToLines(string _s, int lineW)
    {
        var split = _s.Replace("\r", "").Split('\n');
        if (_s.Length <= lineW)
            return split;
        
        List<string> lines = new();
        for (int spi = 0; spi < split.Length; spi++)
        {
            string s = split[spi];
            int linesCount = s.Length / lineW;
            if (s.Length % lineW != 0)
                linesCount++;

            for (int i = 0; i < linesCount; i++)
                lines.Add(s.Substring(i * lineW, Math.Min(lineW, s.Length - i * lineW)));
        }
        
        return lines;
    }

    public void Dispose()
    {
        StopUpdating();
    }
}