using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BlockadeLabsSDK;
using UnityEditor;
using UnityEngine;

public class BlockadeImaginarium : MonoBehaviour
{
    [Tooltip("API Key from Blockade Labs")]
    [SerializeField]
    public string apiKey;

    [Tooltip("Specifies if in-game GUI should be displayed")]
    [SerializeField]
    public bool enableGUI = false;

    [Tooltip("Specifies if in-game Skybox GUI should be displayed")]
    [SerializeField]
    public bool enableSkyboxGUI = false;

    [Tooltip("Specifies if the result should automatically be assigned as the sprite of the current game objects sprite renderer")]
    [SerializeField]
    public bool assignToSpriteRenderer = true;

    [Tooltip("Specifies if the result should automatically be assigned as the texture of the current game objects renderer material")]
    [SerializeField]
    public bool assignToMaterial = false;

    [Tooltip("The result image")]
    [SerializeField]
    public Texture2D resultImage;

    [Tooltip("The result depth")]
    [SerializeField]
    public Texture2D resultDepth;

    public Texture2D previewImage { get; set; }
    public List<GeneratorField> generatorFields = new List<GeneratorField>();
    public List<SkyboxStyleField> skyboxStyleFields = new List<SkyboxStyleField>();
    public List<Generator> generators = new List<Generator>();
    public List<SkyboxStyle> skyboxStyles = new List<SkyboxStyle>();
    public string[] generatorOptions;
    public string[] skyboxStyleOptions;
    public int generatorOptionsIndex = 0;
    public int skyboxStyleOptionsIndex = 0;
    public int lastGeneratorOptionsIndex = 0;
    public int lastSkyboxStyleOptionsIndex = 0;
    public string imagineObfuscatedId = "";
    private int progressId;
    GUIStyle guiStyle;

    public bool SaveAsAssets = true;
    public bool SaveAsImages = true;

    [HideInInspector]
    private float percentageCompleted = -1;
    private bool isCancelled;

    public void OnGUI()
    {
        if (enableGUI)
        {
            DrawGUILayout();
        }
        else if (enableSkyboxGUI)
        {
            DrawSkyboxGUILayout();
        }
    }

    private void DrawSkyboxGUILayout()
    {
        DefineStyles();

        GUILayout.BeginArea(new Rect(Screen.width - (Screen.width / 3), 0, 300, Screen.height), guiStyle);

        if (GUILayout.Button("Get Styles"))
        {
            _ = GetSkyboxStyleOptions();
        }

        // Iterate over skybox fields and render them
        if (skyboxStyleFields.Count > 0)
        {
            RenderSkyboxInGameFields();
        }

        GUILayout.EndArea();
    }

    private void DrawGUILayout()
    {
        DefineStyles();

        GUILayout.BeginArea(new Rect(Screen.width - (Screen.width / 3), 0, 300, Screen.height), guiStyle);

        if (GUILayout.Button("Get Generators"))
        {
            _ = GetGeneratorsWithFields();
        }

        // Iterate over generator fields and render them
        if (generatorFields.Count > 0)
        {
            RenderInGameFields();
        }

        GUILayout.EndArea();
    }

    private void RenderSkyboxInGameFields()
    {
        GUILayout.BeginVertical("Box");
        skyboxStyleOptionsIndex = GUILayout.SelectionGrid(skyboxStyleOptionsIndex, skyboxStyleOptions, 1);
        GUILayout.EndVertical();

        if (skyboxStyleOptionsIndex != lastSkyboxStyleOptionsIndex) {
            GetSkyboxStyleFields(skyboxStyleOptionsIndex);
            lastSkyboxStyleOptionsIndex = skyboxStyleOptionsIndex;
        }

        foreach (var field in skyboxStyleFields)
        {
            // Begin horizontal layout
            GUILayout.BeginHorizontal();

            // Create label for field
            GUILayout.Label(field.name + "*");

            // Create text field for field value
            field.value = GUILayout.TextField(field.value);

            // End horizontal layout
            GUILayout.EndHorizontal();
        }

        if (PercentageCompleted() >= 0 && PercentageCompleted() < 100)
        {
            if (GUILayout.Button("Cancel (" + PercentageCompleted() + "%)"))
            {
                Cancel();
            }
        }
        else
        {
            if (GUILayout.Button("Generate"))
            {
                _ = InitializeSkyboxGeneration(skyboxStyleFields, skyboxStyles[skyboxStyleOptionsIndex].id, true);
            }
        }
    }

    private void RenderInGameFields()
    {
        GUILayout.BeginVertical("Box");
        generatorOptionsIndex = GUILayout.SelectionGrid(generatorOptionsIndex, generatorOptions, 1);
        GUILayout.EndVertical();

        if (generatorOptionsIndex != lastGeneratorOptionsIndex) {
            GetGeneratorFields(generatorOptionsIndex);
            lastGeneratorOptionsIndex = generatorOptionsIndex;
        }

        foreach (GeneratorField field in generatorFields)
        {
            // Begin horizontal layout
            GUILayout.BeginHorizontal();

            var required = field.required ? "*" : "";
            // Create label for field
            GUILayout.Label(field.key + required);


            if (field.param.type == "switch")
            {
                // draw a checkbox
                field.value = GUILayout.Toggle(field.value == "true", new GUIContent
                {
                    text = ""
                }) ? "true" : "false";

                // End horizontal layout
                GUILayout.EndHorizontal();
            }
            else if (field.param.type == "select")
            {
                // End horizontal layout
                GUILayout.EndHorizontal();

                // generate a dropdown select for field.param.options
                GUILayout.BeginVertical("Box");

                // empty array
                string[] options_array_as_strings = new string[field.param.options.Count()];

                int i = 0;
                foreach (ParamOption option in field.param.options)
                {
                    options_array_as_strings[i] = option.label;
                    i++;
                }

                field.selectedIndex = GUILayout.SelectionGrid(
                    field.selectedIndex,
                    options_array_as_strings,
                    1
                );

                GUILayout.EndVertical();
            }
            else
            {
                // Create text field for field value
                field.value = GUILayout.TextField(field.value);

                // End horizontal layout
                GUILayout.EndHorizontal();
            }


        }

        if (PercentageCompleted() >= 0 && PercentageCompleted() < 100)
        {
            if (GUILayout.Button("Cancel (" + PercentageCompleted() + "%)"))
            {
                Cancel();
            }
        }
        else
        {
            if (GUILayout.Button("Generate"))
            {
                _ = InitializeGeneration(generatorFields, generators[generatorOptionsIndex].generator, true);
            }
        }
    }

    private void DefineStyles()
    {
        guiStyle = new GUIStyle();
        guiStyle.fontSize = 20;
        guiStyle.normal.textColor = Color.white;
        guiStyle.normal.background = new Texture2D(1, 1);
        guiStyle.normal.background.SetPixel(0, 0, Color.black);
        guiStyle.normal.background.Apply();
        guiStyle.margin = new RectOffset(20, 20, 20, 20);
        guiStyle.padding = new RectOffset(20, 20, 20, 20);
    }

    public async Task GetSkyboxStyleOptions()
    {
        skyboxStyles = await ApiRequests.GetSkyboxStyles(apiKey);
        skyboxStyleOptions = skyboxStyles.Select(s => s.name).ToArray();

        GetSkyboxStyleFields(skyboxStyleOptionsIndex);
    }

    public void GetSkyboxStyleFields(int index)
    {
        skyboxStyleFields = new List<SkyboxStyleField>();

        // add the default prompt field
        var promptField = new SkyboxStyleField(
            new UserInput(
                "prompt",
                1,
                "prompt",
                ""
            )
        );
        skyboxStyleFields.Add(promptField);
    }

    public async Task GetGeneratorsWithFields()
    {
        // Get the Style Options
        await GetSkyboxStyleOptions();

        generators = await ApiRequests.GetGenerators(apiKey);
        generatorOptions = generators.Select(s => s.generator).ToArray();

        GetGeneratorFields(generatorOptionsIndex);
    }

    public void GetGeneratorFields(int index)
    {
        generatorFields = new List<GeneratorField>();

        foreach (KeyValuePair<string, Param> fieldData in generators[index].@params)
        {
            Debug.Log("Generator Field: " + fieldData.Key + " " + fieldData.Value.type + " : " + fieldData.Value.default_value);

            var field = new GeneratorField(fieldData);
            // default return_depth to true
            if (fieldData.Key == "return_depth") {
                field.value = "true";
            }
            generatorFields.Add(field);
        }

        var skybox_style_id_param = new Param();
        skybox_style_id_param.name = "Skybox Style ID";
        skybox_style_id_param.type = "select";
        skybox_style_id_param.default_value = "5";
        skybox_style_id_param.options = new ParamOption[skyboxStyles.Count()];

        int i = 0;
        foreach(var option in skyboxStyles)
        {
            skybox_style_id_param.options[i] = new ParamOption
            {
                label = option.name,
                value = option.id.ToString() + ": " + option.name
            };
            i++;
        }

        var style_field = new GeneratorField(new KeyValuePair<string, Param>("skybox_style_id", skybox_style_id_param));
        generatorFields.Add(style_field);
    }

    public async Task InitializeSkyboxGeneration(List<SkyboxStyleField> skyboxStyleFields, int id, bool runtime = false)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Debug.Log("You need to provide an Api Key in api options.");
            return;
        }

        isCancelled = false;
        await CreateSkybox(skyboxStyleFields, id, runtime);
    }

    async Task CreateSkybox(List<SkyboxStyleField> skyboxStyleFields, int id, bool runtime = false)
    {
        percentageCompleted = 1;

        #if UNITY_EDITOR
            progressId = Progress.Start("Generating Skybox Assets");
        #endif

        var createSkyboxObfuscatedId = await ApiRequests.CreateSkybox(skyboxStyleFields, id, apiKey);

        InitializeGetAssets(runtime, createSkyboxObfuscatedId);
    }

    public async Task InitializeGeneration(List<GeneratorField> generatorFields, string generator, bool runtime = false)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Debug.Log("You need to provide an Api Key in api options.");
            return;
        }



        isCancelled = false;
        await CreateImagine(generatorFields, generator, runtime);
    }

    async Task CreateImagine(List<GeneratorField> generatorFields, string generator, bool runtime = false)
    {
        percentageCompleted = 1;

        #if UNITY_EDITOR
            progressId = Progress.Start("Generating Assets");
        #endif

        var createImagineObfuscatedId = await ApiRequests.CreateImagine(generatorFields, generator, apiKey);

        InitializeGetAssets(runtime, createImagineObfuscatedId);
    }

    private void InitializeGetAssets(bool runtime, string createImagineObfuscatedId)
    {
        if (createImagineObfuscatedId != "")
        {
            imagineObfuscatedId = createImagineObfuscatedId;
            percentageCompleted = 33;
            CalculateProgress();

            var pusherManager = false;

            #if PUSHER_PRESENT
                pusherManager = FindObjectOfType<PusherManager>();
            #endif

            if (pusherManager && runtime)
            {
                #if PUSHER_PRESENT
                    _ = PusherManager.instance.SubscribeToChannel(imagineObfuscatedId);
                #endif
            }
            else
            {
                _ = GetAssets();
            }
        }
    }

    public async Task GetAssets()
    {
        var textureUrl = "";
        var prompt = "";
        var count = 0;

        while (!isCancelled)
        {
            #if UNITY_EDITOR
                EditorUtility.SetDirty(this);
            #endif

            await Task.Delay(1000);

            if (isCancelled)
            {
                break;
            }

            count++;

            var getImagineResult = await ApiRequests.GetImagine(imagineObfuscatedId, apiKey);

            // Debug Log the result (loop over Dictionary<string, string>)
            foreach (KeyValuePair<string, string> kvp in getImagineResult)
            {
                Debug.Log(kvp.Key + ": " + kvp.Value);
            }

            if (getImagineResult.Count > 0)
            {
                percentageCompleted = 66;
                CalculateProgress();
                textureUrl = getImagineResult["textureUrl"];
                prompt = getImagineResult["prompt"];
                break;
            }
        }

        if (isCancelled)
        {
            percentageCompleted = -1;
            DestroyImmediate(previewImage);
            imagineObfuscatedId = "";
            return;
        }

        if (!string.IsNullOrWhiteSpace(textureUrl))
        {
            var image = await ApiRequests.GetImagineImage(textureUrl);

            string depthUrl = textureUrl.Replace("images/", "depths/");
            var depth_image = await ApiRequests.GetImagineImage(depthUrl);

            var texture = new Texture2D(512, 512, TextureFormat.RGB24, false);
            texture.LoadImage(image);
            resultImage = texture;

            var previewTexture = new Texture2D(128, 128, TextureFormat.RGB24, false);
            previewTexture.LoadImage(image);

            var depth_texture = new Texture2D(512, 512, TextureFormat.RGB24, false);
            depth_texture.LoadImage(depth_image);
            resultDepth = depth_texture;

            percentageCompleted = 80;
            CalculateProgress();

            if (previewImage != null)
            {
                DestroyImmediate(previewImage);
                previewImage = null;
            }

            previewImage = previewTexture;

            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));

            if (assignToMaterial)
            {
                var r = GetComponent<Renderer>();
                if (r != null)
                {
                    if (r.sharedMaterial != null)
                    {
                        r.sharedMaterial.mainTexture = texture;
                        r.sharedMaterial.SetTexture("_Depth", depth_texture);
                    }
                }
            }

            if (assignToSpriteRenderer)
            {
                var spriteRenderer = GetComponent<SpriteRenderer>();

                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = sprite;
                }
            }

            percentageCompleted = 90;
            CalculateProgress();
            SaveAssets(texture, depth_texture, sprite, prompt);
        }

        percentageCompleted = 100;
        CalculateProgress();
        #if UNITY_EDITOR
            Progress.Remove(progressId);
        #endif
    }

    private void SaveAssets(Texture2D texture, Texture2D depth_texture, Sprite sprite, string prompt)
    {
        #if UNITY_EDITOR
            if (AssetDatabase.Contains(texture) && AssetDatabase.Contains(sprite))
            {
                Debug.Log("Texture already in assets database.");
                return;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Blockade Labs SDK Assets"))
            {
                AssetDatabase.CreateFolder("Assets", "Blockade Labs SDK Assets");
            }

            var maxLength = 20;

            if (prompt.Length > maxLength)
            {
                prompt = prompt.Substring(0, maxLength);
            }

            var textureName = ValidateFilename(prompt) + "_texture";
            var spriteName = ValidateFilename(prompt) + "_sprite";

            var counter = 0;

            while (true)
            {
                var modifiedTextureName = counter == 0 ? textureName : textureName + "_" + counter;
                var modifiedSpriteName = counter == 0 ? spriteName : spriteName + "_" + counter;

                var textureAssets = AssetDatabase.FindAssets(modifiedTextureName, new[] { "Assets/Blockade Labs SDK Assets" });

                if (textureAssets.Length > 0)
                {
                    counter++;
                    continue;
                }

                if(SaveAsAssets){
                    AssetDatabase.CreateAsset(texture, "Assets/Blockade Labs SDK Assets/" + modifiedTextureName + ".asset");
                    AssetDatabase.CreateAsset(sprite, "Assets/Blockade Labs SDK Assets/" + modifiedSpriteName + ".asset");
                    AssetDatabase.CreateAsset(depth_texture, "Assets/Blockade Labs SDK Assets/" + modifiedTextureName + "_depth.asset");
                }

                if(SaveAsImages){

                    string save_as = "jpg";

                    // TODO: loop through generatorOptions, find the one with the key image_type and if it's selectedIndex is the same index as the option with value "png" then change save_as to "png"

                    if(save_as == "jpg")
                    {
                        // Save the texture Texture2D to a png image
                        var bytes = texture.EncodeToJPG();
                        File.WriteAllBytes(Application.dataPath + "/Blockade Labs SDK Assets/" + modifiedTextureName + ".jpg", bytes);

                        // Save the depth_texture Texture2D to a jpg image
                        var bytes_depth = depth_texture.EncodeToJPG();
                        File.WriteAllBytes(Application.dataPath + "/Blockade Labs SDK Assets/" + modifiedTextureName + "_depth.jpg", bytes_depth);
                    }
                    else
                    {
                        // Save the texture Texture2D to a png image
                        var bytes = texture.EncodeToPNG();
                        File.WriteAllBytes(Application.dataPath + "/Blockade Labs SDK Assets/" + modifiedTextureName + ".png", bytes);

                        // Save the depth_texture Texture2D to a jpg image
                        var bytes_depth = depth_texture.EncodeToPNG();
                        File.WriteAllBytes(Application.dataPath + "/Blockade Labs SDK Assets/" + modifiedTextureName + "_depth.png", bytes_depth);
                    }

                }
                break;
            }
        #endif

        imagineObfuscatedId = "";
    }

    private string ValidateFilename(string prompt)
    {
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
        {
            prompt = prompt.Replace(c, '_');
        }

        while (prompt.Contains("__"))
        {
            prompt = prompt.Replace("__", "_");
        }

        return prompt.TrimStart('_').TrimEnd('_');
    }

    private void CalculateProgress()
    {
        #if UNITY_EDITOR
            Progress.Report(progressId, percentageCompleted / 100f);
        #endif
    }

    public float PercentageCompleted() => percentageCompleted;

    public void Cancel()
    {
        isCancelled = true;
        percentageCompleted = -1;
        #if UNITY_EDITOR
            Progress.Remove(progressId);
        #endif
    }
}