using Microsoft.AspNetCore.Components.Forms;
using Newtonsoft.Json;
using System.Text;

namespace funscript_web_app;

public static class funscript_manager
{

    public static Funscript? starting_funscript { get; set; }

    public static Funscript? modified_funscript { get; set; }

    private static bool reset_trigger_canvas_rerender;

    public static bool Reset_trigger_canvas_rerender
    {

        get { return reset_trigger_canvas_rerender; }
        set
        {
            if (reset_trigger_canvas_rerender != value)
            {
                reset_trigger_canvas_rerender = value;
                rerender_canvas_event?.Invoke();
            }
        }

    }
    public delegate void RerenderforCanvas();

    public static event RerenderforCanvas rerender_canvas_event;

    public static bool user_entered_funscript;
    public static bool User_entered_funscript
    {
        get { return user_entered_funscript; }
        set
        {
            if (user_entered_funscript != value)
            {
                user_entered_funscript = value;
                // Raise the event when the value changes
                RerenderNeededEvent?.Invoke();
            }
        }
    }

    public static bool user_Applied_modifier;
    public static bool User_Applied_modifier
    {
        get { return user_Applied_modifier; }
        set
        {
            if (user_Applied_modifier != value)
            {
                user_Applied_modifier = value;
                // Raise the event when the value changes
                RerenderNeededEvent?.Invoke();
            }
        }
    }

    public delegate void RerenderNeededEventHandler();

    // Define the event using the delegate type
    public static event RerenderNeededEventHandler RerenderNeededEvent;
    public static async Task<Funscript> DeserializeJsonFile(IBrowserFile file)
    {
        Funscript result = new Funscript();

        if (file != null)
        {
            using (var memoryStream = new MemoryStream())
            {
                await file.OpenReadStream().CopyToAsync(memoryStream);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);
                memoryStream.Seek(0, SeekOrigin.Begin);

                using (var reader = new StreamReader(memoryStream))
                {
                    var jsonContent = await reader.ReadToEndAsync();
                    result = JsonConvert.DeserializeObject<Funscript>(jsonContent);
                    result.title = fileNameWithoutExtension;

                }
            }
        }

        return result;
    }

public static (byte[], string) serialise()
    {

        string json = JsonConvert.SerializeObject(modified_funscript);

        byte[] fileData = Encoding.UTF8.GetBytes(json);

        string fileName = $"{modified_funscript.title}.funscript";

        return (fileData, fileName);


    }
}

