using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PaintScript_Engine.Versions;

public class PaintScriptEngine_Alpha_0_1_0
{
    // =========================
    // Core IR model
    // =========================

    public sealed class PSProgram
    {
        public string Version { get; set; } = "";
        public List<PSTarget> Targets { get; set; } = new();
        public Dictionary<string, PSGlobalVariable> Globals { get; set; } = new();
    }

    public sealed class PSTarget
    {
        public string Name { get; set; } = "";
        public string Instance { get; set; } = "";

        public Dictionary<string, PSVariable> Variables { get; set; } = new();
        public Dictionary<string, PSFunction> Functions { get; set; } = new();

        // eventName -> list of handlers, each handler is a list of instructions
        public Dictionary<string, List<List<PSInstruction>>> Events { get; set; } = new();
    }

    public sealed class PSValue
    {
        public string Type { get; set; } = "*"; // PaintScript type: "Number", "String", "Boolean", "*", etc.
        public object? Value { get; set; }
    }

    public class PSVariable
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "*";
        public PSValue Value { get; set; } = new();
        public bool IsPublic { get; set; }
    }

    public sealed class PSGlobalVariable : PSVariable { }

    public sealed class PSParameter
    {
        public string Name { get; set; } = "";
        public string Type { get; set; } = "*";
    }

    public sealed class PSFunction
    {
        public string Name { get; set; } = "";
        public bool Strict { get; set; }
        public List<PSParameter> Parameters { get; set; } = new();
        public string ReturnType { get; set; } = "*";
        public List<PSInstruction> Code { get; set; } = new();
    }

    public sealed class PSInstruction
    {
        public string Op { get; set; } = ""; // "var", "assign", "call", "if", "repeat", "while", "forever", etc.
        public Dictionary<string, object?> Fields { get; set; } = new(); // condition, code, args, name, etc.
    }

    // =========================
    // Expression IR
    // =========================

    public sealed class PSValueExpr
    {
        public string Kind { get; set; } = ""; // "literal", "variable", "expression", "call"
        public string Type { get; set; } = "*";
        public object? Value { get; set; } // for literal, variable name, function name, etc.
        public string Op { get; set; } = ""; // for expression: "+", "==", "&&", etc.
        public List<PSValueExpr> Args { get; set; } = new(); // expression args or call args
    }

    // =========================
    // Thread + Engine
    // =========================

    public sealed class PaintScriptThread
    {
        public PSTarget Target { get; }
        public List<PSInstruction> Code { get; }
        public int Ip { get; private set; } = 0;

        public bool IsFinished { get; private set; }
        public bool IsWaiting { get; set; }
        public PSValueExpr? WaitUntilCondition { get; set; }
        public DateTime? WakeAt { get; set; }

        public List<PaintScriptThread> WaitingForThreads { get; set; } = new();

        public Dictionary<string, PSValue> Locals { get; } = new();

        public PaintScriptThread(PSTarget target, List<PSInstruction> code)
        {
            Target = target;
            Code = code;
        }

        public void Reset() => Ip = 0;

        public void Step(PaintScriptEngine engine)
        {
            if (IsFinished || IsWaiting) return;
            if (Ip < 0 || Ip >= Code.Count)
            {
                IsFinished = true;
                return;
            }

            var instr = Code[Ip++];
            engine.ExecuteInstruction(this, instr);
        }
    }

    public sealed class PaintScriptEngine
    {
        public PSProgram Program { get; }
        public List<PaintScriptThread> Threads { get; } = new();

        public PaintScriptEngine(PSProgram program)
        {
            Program = program;
        }

        public void StartEvent(PSTarget target, string eventName)
        {
            if (!target.Events.TryGetValue(eventName, out var handlers)) return;

            foreach (var handler in handlers)
            {
                var thread = new PaintScriptThread(target, handler);
                Threads.Add(thread);
            }
        }

        public async Task TickAsync()
        {
            foreach (var thread in Threads.ToArray())
            {
                // Timed wait
                if (thread.IsWaiting && thread.WakeAt is DateTime t && t <= DateTime.UtcNow)
                    thread.IsWaiting = false;

                // Conditional wait
                if (thread.IsWaiting && thread.WaitUntilCondition != null)
                {
                    var cond = Convert.ToBoolean(EvaluateValue(thread, thread.WaitUntilCondition));
                    if (cond)
                    {
                        thread.IsWaiting = false;
                        thread.WaitUntilCondition = null;
                    }
                }

                // BroadcastAndWait: check if all child threads finished
                if (thread.IsWaiting && thread.WaitingForThreads.Count > 0)
                {
                    bool allDone = true;

                    foreach (var child in thread.WaitingForThreads)
                    {
                        if (!child.IsFinished)
                        {
                            allDone = false;
                            break;
                        }
                    }

                    if (allDone)
                    {
                        thread.IsWaiting = false;
                        thread.WaitingForThreads.Clear();
                    }
                }

                // Run thread if not waiting
                if (!thread.IsWaiting && !thread.IsFinished)
                    thread.Step(this);
            }

            await Task.Yield();
        }

        public void ExecuteInstruction(PaintScriptThread thread, PSInstruction instr)
        {
            switch (instr.Op)
            {
                case "var":
                    ExecVar(thread, instr);
                    break;
                case "assign":
                    ExecAssign(thread, instr);
                    break;
                case "call":
                    ExecCall(thread, instr);
                    break;
                case "if":
                    ExecIf(thread, instr);
                    break;
                case "repeat":
                    ExecRepeat(thread, instr);
                    break;
                case "repeat_until":
                    ExecRepeatUntil(thread, instr);
                    break;
                case "while":
                    ExecWhile(thread, instr);
                    break;
                case "forever":
                    ExecForever(thread, instr);
                    break;
                case "wait":
                    ExecWait(thread, instr);
                    break;
                case "wait_until":
                    ExecWaitUntil(thread, instr);
                    break;
                case "broadcast":
                    ExecBroadcast(thread, instr);
                    break;
                case "broadcast_wait":
                    ExecBroadcastWait(thread, instr);
                    break;
                case "event_call":
                    ExecEventCall(thread, instr);
                    break;
                    // etc...
            }
        }

        // =========================
        // Core exec helpers
        // =========================

        private void ExecVar(PaintScriptThread thread, PSInstruction instr)
        {
            var name = (string)instr.Fields["name"]!;
            var type = (string)instr.Fields["type"]!;
            var valueExpr = (PSValueExpr)instr.Fields["value"]!;

            var value = EvaluateValue(thread, valueExpr);
            thread.Locals[name] = new PSValue { Type = type, Value = value };
        }

        private void ExecAssign(PaintScriptThread thread, PSInstruction instr)
        {
            var name = (string)instr.Fields["target"]!;
            var valueExpr = (PSValueExpr)instr.Fields["value"]!;
            var value = EvaluateValue(thread, valueExpr);

            // 1. Local
            if (thread.Locals.TryGetValue(name, out var local))
            {
                local.Value = value;
                return;
            }

            // 2. Sprite variable
            if (thread.Target.Variables.TryGetValue(name, out var spriteVar))
            {
                spriteVar.Value.Value = value;
                return;
            }

            // 3. Global variable
            if (Program.Globals.TryGetValue(name, out var globalVar))
            {
                globalVar.Value.Value = value;
                return;
            }

            // 4. Not found → create local (dynamic)
            thread.Locals[name] = new PSValue { Type = "*", Value = value };
        }

        private void ExecWait(PaintScriptThread thread, PSInstruction instr)
        {
            var durationExpr = (PSValueExpr)instr.Fields["duration"]!;
            var ms = Convert.ToInt32(EvaluateValue(thread, durationExpr));
            thread.IsWaiting = true;
            thread.WakeAt = DateTime.UtcNow.AddMilliseconds(ms);
        }

        private void ExecWaitUntil(PaintScriptThread thread, PSInstruction instr)
        {
            var condExpr = (PSValueExpr)instr.Fields["condition"]!;

            thread.IsWaiting = true;
            thread.WaitUntilCondition = condExpr;
            thread.WakeAt = null; // ensure it's not treated as a timed wait
        }

        private void ExecEventCall(PaintScriptThread thread, PSInstruction instr)
        {
            var name = (string)instr.Fields["name"]!;
            var target = thread.Target;
            StartEvent(target, name.TrimStart('@'));
        }

        private void ExecCall(PaintScriptThread thread, PSInstruction instr)
        {
            var fnName = (string)instr.Fields["name"]!;
            var argsExprs = (List<PSValueExpr>)instr.Fields["args"]!;

            var target = thread.Target;
            if (!target.Functions.TryGetValue(fnName, out var fn))
                return; // or throw

            var newThread = new PaintScriptThread(target, fn.Code);
            // simple parameter binding
            for (int i = 0; i < fn.Parameters.Count && i < argsExprs.Count; i++)
            {
                var p = fn.Parameters[i];
                var v = EvaluateValue(thread, argsExprs[i]);
                newThread.Locals[p.Name] = new PSValue { Type = p.Type, Value = v };
            }

            Threads.Add(newThread);
        }

        private void ExecIf(PaintScriptThread thread, PSInstruction instr)
        {
            var condExpr = (PSValueExpr)instr.Fields["condition"]!;
            var thenCode = (List<PSInstruction>)instr.Fields["then"]!;
            var elseCode = instr.Fields.TryGetValue("else", out var e) && e is List<PSInstruction> ec ? ec : null;

            var cond = Convert.ToBoolean(EvaluateValue(thread, condExpr));
            if (cond)
            {
                var inner = new PaintScriptThread(thread.Target, thenCode);
                Threads.Add(inner);
            }
            else if (elseCode != null)
            {
                var inner = new PaintScriptThread(thread.Target, elseCode);
                Threads.Add(inner);
            }
        }

        private void ExecRepeat(PaintScriptThread thread, PSInstruction instr)
        {
            var countExpr = (PSValueExpr)instr.Fields["count"]!;
            var code = (List<PSInstruction>)instr.Fields["code"]!;
            var count = Convert.ToInt32(EvaluateValue(thread, countExpr));

            for (int i = 0; i < count; i++)
            {
                var inner = new PaintScriptThread(thread.Target, code);
                Threads.Add(inner);
            }
        }

        private void ExecRepeatUntil(PaintScriptThread thread, PSInstruction instr)
        {
            var condExpr = (PSValueExpr)instr.Fields["condition"]!;
            var code = (List<PSInstruction>)instr.Fields["code"]!;

            // Flip condition
            while (!Convert.ToBoolean(EvaluateValue(thread, condExpr)))
            {
                var inner = new PaintScriptThread(thread.Target, code);
                inner.Step(this);
            }
        }

        private void ExecWhile(PaintScriptThread thread, PSInstruction instr)
        {
            var condExpr = (PSValueExpr)instr.Fields["condition"]!;
            var code = (List<PSInstruction>)instr.Fields["code"]!;

            while (Convert.ToBoolean(EvaluateValue(thread, condExpr)))
            {
                var inner = new PaintScriptThread(thread.Target, code);
                inner.Step(this);
            }
        }

        private void ExecForever(PaintScriptThread thread, PSInstruction instr)
        {
            var code = (List<PSInstruction>)instr.Fields["code"]!;
            // simple: re-run code every Tick
            var inner = new PaintScriptThread(thread.Target, code);
            Threads.Add(inner);
        }

        private void ExecBroadcast(PaintScriptThread thread, PSInstruction instr)
        {
            var msg = (string)instr.Fields["message"]!;
            foreach (var target in Program.Targets)
            {
                if (!target.Events.TryGetValue("receive", out var handlers)) continue;

                foreach (var handler in handlers)
                {
                    var t = new PaintScriptThread(target, handler);
                    Threads.Add(t);
                }
            }
        }

        private void ExecBroadcastWait(PaintScriptThread thread, PSInstruction instr)
        {
            var msg = (string)instr.Fields["message"]!;
            var waitingList = new List<PaintScriptThread>();

            // Start all message handlers
            foreach (var target in Program.Targets)
            {
                if (!target.Events.TryGetValue("receive", out var handlers))
                    continue;

                foreach (var handler in handlers)
                {
                    var t = new PaintScriptThread(target, handler);
                    Threads.Add(t);
                    waitingList.Add(t);
                }
            }

            // Pause this thread until all child threads finish
            thread.IsWaiting = true;
            thread.WaitingForThreads = waitingList;
        }


        // =========================
        // Expression evaluation
        // =========================

        private object? EvaluateValue(PaintScriptThread thread, PSValueExpr expr)
        {
            switch (expr.Kind)
            {
                case "literal":
                    return expr.Value;

                case "variable":
                    {
                        var name = (string)expr.Value!;
                        return ResolveVariable(thread, name);
                    }

                case "expression":
                    return EvaluateExpression(thread, expr);

                case "call":
                    // simple: only support built-ins like say() here
                    return null;

                default:
                    return null;
            }
        }

        private object? EvaluateExpression(PaintScriptThread thread, PSValueExpr expr)
        {
            var op = expr.Op;
            var args = expr.Args;

            object? a = args.Count > 0 ? EvaluateValue(thread, args[0]) : null;
            object? b = args.Count > 1 ? EvaluateValue(thread, args[1]) : null;

            return op switch
            {
                "add" or "+" => $"{a}{b}",
                "==" => Equals(a, b),
                "!=" => !Equals(a, b),
                ">" => Convert.ToDouble(a) > Convert.ToDouble(b),
                "<" => Convert.ToDouble(a) < Convert.ToDouble(b),
                ">=" => Convert.ToDouble(a) >= Convert.ToDouble(b),
                "<=" => Convert.ToDouble(a) <= Convert.ToDouble(b),
                "&&" => Convert.ToBoolean(a) && Convert.ToBoolean(b),
                "||" => Convert.ToBoolean(a) || Convert.ToBoolean(b),
                "not" => !Convert.ToBoolean(a),
                _ => null
            };
        }

        // =========================
        // Resolve Variables
        // =========================

        private object? ResolveVariable(PaintScriptThread thread, string name)
        {
            // 1. Local variables
            if (thread.Locals.TryGetValue(name, out var local))
                return local.Value;

            // 2. Sprite variables
            if (thread.Target.Variables.TryGetValue(name, out var spriteVar))
                return spriteVar.Value.Value;

            // 3. Global variables
            if (Program.Globals.TryGetValue(name, out var globalVar))
                return globalVar.Value.Value;

            // 4. Not found → dynamic null
            return null;
        }

    }
}
