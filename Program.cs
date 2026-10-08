using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using PaintScript_Engine.Versions;

namespace PaintScript_Engine
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            string jsonPath = args.Length > 0 ? args[0] : "program.json";
            if (!File.Exists(jsonPath) && File.Exists("program1.json"))
                jsonPath = "program1.json";

            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"PaintScript program file not found: {jsonPath}");
                return;
            }

            string json = File.ReadAllText(jsonPath);
            var program = PaintScriptEngine_Alpha_0_1_1.LoadProgram(json);

            if (program == null)
            {
                Console.WriteLine("Failed to load PaintScript program.");
                return;
            }

            var engine = new PaintScriptEngine_Alpha_0_1_0.PaintScriptEngine(program);

            // Start @start event on all targets
            foreach (var target in program.Targets)
            {
                engine.StartEvent(target, "Start");
            }

            // Tick loop
            Console.WriteLine("Running PaintScript program...");
            while (true)
            {
                await engine.TickAsync();
                await Task.Delay(10);
            }
        }
    }
}
