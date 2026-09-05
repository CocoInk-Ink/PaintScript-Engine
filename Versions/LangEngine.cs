using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PaintScript_Engine.Versions;

public class LangEngine
{
    public dynamic program;
    public Dictionary<string, Message> MessageHandlers { get; internal set; }

    protected List<Target> targets => PaintScriptEngine.Main.Targets;

    public virtual void Tick() {}

    public LangEngine(string json) {
        program = JsonSerializer.Deserialize<ExpandoObject>(json);
    }
}