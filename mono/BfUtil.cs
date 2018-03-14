using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public class BfUtil {
  public static string LoadProgram(string fileName) {
    var sr = new StreamReader(fileName, Encoding.GetEncoding("utf-8"));
    string text = ParseFromStream(sr);
    sr.Close();
    return text;
  }

  private static string ParseFromStream(StreamReader sr) {
    List<char> chars = new List<char>();
    while (!sr.EndOfStream) {
      int c = sr.Read();
      if (c == '>' || c == '<' || c == '+' || c == '-' || c == '.' ||
          c == ',' || c == '[' || c == ']') {
        chars.Add((char)c);
      }
    }
    return new string(chars.ToArray());
  }

  public static void DIE(string message) {
    Console.Error.WriteLine(message);
    Environment.Exit(1);
  }

  public static void PutChar(char c) {
    Console.Write(c);
  }

  public static char GetChar() {
    int c = Console.Read();
    if (c == -1)  // EOF
      c = 0;
    return (char)(c & 255);
  }

  public static void PutInt(int i) {
    Console.WriteLine("Putint[" + i + "]");
  }
}
