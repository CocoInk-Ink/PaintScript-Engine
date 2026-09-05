using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text.Json;

using PaintScript_Engine.Versions;

namespace PaintScript_Engine;

public class Program
{
    public static void Main(string[] args)
    {
        var engine = new PaintScriptEngine();

        engine.PreLaunch(args);

        while (true)
        {
            engine.Tick();
        }
    }
}

public class PaintScriptEngine
{
    public static PaintScriptEngine Main;
    public static string Version = "1.0.0";
    public static string Name = "PaintScript";

    public static string[] SupportedLangVersions = new string[] { "Alpha 0.1.0" };
    public string LangVersion = "Alpha 0.1.0";
    public List<Target> Targets = new List<Target>();

    public Point StageSize = new Point(640, 450);

    LangEngine engine;

    public Dictionary<string, Message> MessageHandlers => engine.MessageHandlers;

    public PaintScriptEngine() {
        Main = this;
    }

    public void PreLaunch(string[] args) {
        for (var i = 0; i < args.Length; i++)
        {
            var item = args[i];

            if (item == "--lang-version")
            {
                i++;

                if (!SupportedLangVersions.Contains(args[i]))
                {
                    Console.WriteLine("Unsupported language version.");
                    return;
                }

                this.LangVersion = args[i];
            }
        }

        this.engine = FindLangEngine();
    }

    private LangEngine FindLangEngine() {
        return this.LangVersion switch
        {
            "Alpha 0.1.0" => new Lang_Alpha_0_1_0(),
            _ => throw new Exception("Language version is not supported! Either it's a future version, or a non-existant/unsupported version")
        };
    }

    public void Tick() {
        engine.Tick();
    }
}

public class Log
{
    public static string[] log(params string[] rest)
    {
        foreach (var item in rest)
        {
            log(item);
        }
        return rest;
    }

    public static string log(string item)
    {
#if DEBUG
        Debug.WriteLine(item);
#else
        Console.WriteLine(item);
#endif
        return item;
    }
}