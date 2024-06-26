using System.Drawing;

namespace funscript_web_app;



public static class Canvas_options
{



    public static float TimeToX(int duration_value, Funscript funscript, int width)
    {
        return (duration_value / (float)funscript.lastactionat) * width;
    }

    public static float PosToY(ActionData action, int height)
    {
        return (1f - (action.pos / 100f)) * height;
    }

   /* public static void draw_canvas_lines(ActionData[] actions, Funscript funscript, int width, int height)
    {

        for (int i = 1; i < actions.Length; i++)
        {
            var action = actions[i];
            var prevAction = actions[i - 1];
            int x = (action.At + prevAction.At) / 2;
            x = TimeToX(x, funscript, width);
            int xFloor = (int)Math.Floor(x);

        }

    }*/

}


