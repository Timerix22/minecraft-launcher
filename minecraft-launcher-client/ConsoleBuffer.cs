using System;

namespace launcher_client;

public class ConsoleBuffer
{
    public int Width;
    public int Height;
    
    private int _x, _y;
    private char[][] _buffer;

    public ConsoleBuffer()
    {
        Width = Console.WindowWidth;
        Height = Console.WindowHeight - 1;
        
        _buffer = new char[Height][];
        for(int y = 0; y < Height; y++)
        {
            _buffer[y] = new char[Width];
            for (int x = 0; x < Width; x++)
                _buffer[y][x] = ' ';
        }
    }


    public void SetCursorPosition(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            throw new Exception($"invalid cursor position ({x}, {y}) (width: {Width}, height: {Height})");
        _x = x;
        _y = y;
    }

    public void Write(char c)
    {
        _buffer[_y][_x] = c;
        _x++;
        if (_x == Width)
        {
            _x = 0;
            _y++;
        }
    }
    
    public void Write(string s)
    {
        for(int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            switch (c)
            {
                case '\r':
                    throw new Exception("restricted character: '\\r'");
                case '\n':
                    throw new Exception("restricted character: '\\n'");
                case '\t':
                    Write("    ");
                    break;
                default:
                    Write(c);
                    break;
            }
        }
    }

    public void Print()
    {
        Console.SetCursorPosition(0, 0);
        for (int y = 0; y < Height; y++) 
            Console.Write(new string(_buffer[y]));
        Console.SetCursorPosition(0, 0);
    }
}