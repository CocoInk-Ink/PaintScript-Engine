using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaintScript_Engine;

public class Target
{
    public string InstanceName { get; set; }

    public double X { get; set; }
    public double Y { get; set; }

    // Direction of item
    // Uses degree angles, 0 is up, 90 is right, 180 is down, -180 is left.
    public int Direction { get; set; } = 90;

    public bool IsSprite { get; set; } = true;

    public string Type { get; set; } = "Sprite";

    public Target(string instanceName, double x, double y)
    {
        InstanceName = instanceName;
        X = x;
        Y = y;
    }

}
