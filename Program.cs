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
            // Load the JSON IR (replace with your actual file path or JSON string)
            string json = File.ReadAllText("program.json");

            // Deserialize into PSProgram
            var program = JsonSerializer.Deserialize<PaintScriptEngine_Alpha_0_1_0.PSProgram>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );

            if (program == null)
            {
                Console.WriteLine("Failed to load PaintScript program.");
                return;
            }

            // Create the engine
            var engine = new PaintScriptEngine_Alpha_0_1_0.PaintScriptEngine(program);

            // Start @start event on all targets
            foreach (var target in program.Targets)
            {
                engine.StartEvent(target, "@Start");
            }

            // Tick loop
            Console.WriteLine("Running PaintScript program...");
            while (true)
            {
                await engine.TickAsync();
                await Task.Delay(10); // small delay to avoid CPU burn
            }
        }
    }
}
