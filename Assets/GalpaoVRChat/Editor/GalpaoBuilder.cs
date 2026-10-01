// Galpao VRChat - monta o salao industrial do zero com ProBuilder.
//
// Escala humana para VRChat: o salao tem 26 x 18 x 6,5 m e as mesas de
// cartao 2,2 x 1,4 m a 0,78 m do chao. Mapas Roblox costumam usar escala
// 3 a 4x maior, o que deixaria o jogador um anao dentro do proprio mundo.
//
// Toda a geometria vem das formas do ProBuilder (ShapeGenerator) e recebe
// bevel, que e o que da a leitura de "modelado" em vez de "caixa solta".
// As primitivas do Unity sao cubos crus, sem topologia de aresta.
//
// O visual segue o do GameMap: flat ambient quase preto, sem skybox e
// reflexao em 0,12. A iluminacao vem de pendentes praticas.
//
// Executar: Tools/Galpao VRChat/Build Warehouse
// Reexecutar e seguro: a raiz "Galpao" e recriada do zero e os materiais
// sao reaproveitados, o que mantem o GUID deles estavel no repositorio.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class GalpaoBuilder
{
    const string Root = "Assets/GalpaoVRChat";
    const string MatDir = Root + "/Materials";
    const string FontPath = "Assets/IntroMenuVRChat/UI/Fonts/Oswald-Regular.ttf";

    // Salao
    const float HallX = 13f;   // metade da largura   (26 m)
    const float HallZ = 9f;    // metade da profundidade (18 m)
    const float CeilY = 6.5f;  // pe-direito

    static readonly Color Warm = new Color(1f, 0.86f, 0.66f);
    static readonly Color Mint = new Color(0.45f, 1f, 0.72f);

    [MenuItem("Tools/Galpao VRChat/Build Warehouse")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");

        GameObject existing = GameObject.Find("Galpao");
        if (existing != null) Object.DestroyImmediate(existing);

        UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Galpão")
            Debug.LogWarning("GalpaoBuilder: cena ativa e \"" + scene.name + "\", esperava \"Galpão\".");

        var root = new GameObject("Galpao");
        var shell = Group(root.transform, "Shell");
        var lights = Group(root.transform, "Lighting");
        var shop = Group(root.transform, "Shop");
        var tables = Group(root.transform, "Tables");
        var boards = Group(root.transform, "Leaderboards");
        var props = Group(root.transform, "Props");

        BuildShell(shell);
        BuildLighting(lights);
        BuildShop(shop);
        BuildTables(tables);
        BuildBoards(boards);
        BuildProps(props);
        ApplyRenderSettings();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Galpao: construido e salvo em " + scene.path);
    }

    static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    // ---------------------------------------------------------------- materiais

    // Carrega o material se ja existir, senao cria. Manter o GUID estavel
    // evita diff espurio no git a cada reexecucao do menu.
    static Material Mat(string file, string shader, Color color, float gloss = 0.2f, float metal = 0f, float relief = -1f)
    {
        string path = MatDir + "/" + file + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find(shader);
            if (sh == null) sh = Shader.Find("Standard");
            m = new Material(sh);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", gloss);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
        if (relief >= 0f && m.HasProperty("_Relief")) m.SetFloat("_Relief", relief);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Felt(string file, Color color)
    {
        string path = MatDir + "/" + file + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("TwentyOne/WornFelt"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_Color", color);
        var weave = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Materials/FeltWeave.png");
        if (weave == null) weave = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RoomAtmosphere/Materials/FeltWeave.png");
        if (weave != null && m.HasProperty("_MainTex")) m.SetTexture("_MainTex", weave);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ------------------------------------------------------------------- helpers

    // Aplica o material em TODOS os submeshes. ProBuilder cria um submesh por
    // face e SetMaterial nao cobre todos: o cone de luz, por exemplo, tem
    // submesh 0 (laterais) e 1 (tampa), e deixar o 0 nulo faz o Unity
    // renderizar de magenta, que e a cor de material ausente.
    static void Skin(ProBuilderMesh mesh, Material mat)
    {
        mesh.Refresh();
        var renderer = mesh.GetComponent<MeshRenderer>();
        if (renderer == null) return;
        int count = Mathf.Max(1, renderer.sharedMaterials.Length);
        var mats = new Material[count];
        for (int i = 0; i < count; i++) mats[i] = mat;
        renderer.sharedMaterials = mats;
        EditorUtility.SetDirty(renderer);
    }

    // Bevel em todas as arestas, deduplicadas. E o passo que separa uma caixa
    // solta de um objeto com aresta viva, que pega specular na borda.
    static void Bevel(ProBuilderMesh mesh, float amount)
    {
        if (amount <= 0f) return;
        var edges = new HashSet<Edge>();
        foreach (var f in mesh.faces)
            foreach (var e in f.edges)
                edges.Add(e);
        var list = new List<Edge>(edges);
        if (list.Count == 0) return;
        UnityEngine.ProBuilder.MeshOperations.Bevel.BevelEdges(mesh, list, amount);
        mesh.Refresh();
    }

    // Cube com o pivô no centro: posiciona pelo centro, como primitivas.
    static ProBuilderMesh Block(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, float bevel = 0.02f, Vector3 euler = default, bool collider = false)
    {
        var mesh = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
        mesh.name = name;
        mesh.transform.SetParent(parent, false);
        mesh.transform.localPosition = pos;
        mesh.transform.localEulerAngles = euler;
        Skin(mesh, mat);
        Bevel(mesh, bevel);
        if (collider) mesh.gameObject.AddComponent<MeshCollider>();
        return mesh;
    }

    // ---------------------------------------------------------------- estrutura

    static void BuildShell(Transform p)
    {
        var floor = Mat("GalpaoFloor", "Standard", new Color(0.125f, 0.125f, 0.132f), 0.34f);
        var brick = Mat("GalpaoBrick", "TwentyOne/AgedWallCovering", new Color(0.150f, 0.092f, 0.078f), 0.08f, 0f, 0.55f);
        var concrete = Mat("GalpaoConcrete", "TwentyOne/AgedWallCovering", new Color(0.150f, 0.150f, 0.146f), 0.12f, 0f, 0.35f);
        var ceiling = Mat("GalpaoCeiling", "Standard", new Color(0.040f, 0.040f, 0.045f), 0.10f);
        var steel = Mat("GalpaoSteel", "Standard", new Color(0.095f, 0.095f, 0.105f), 0.55f, 0.85f);

        Block(p, "Floor", new Vector3(0, -0.06f, 0), new Vector3(HallX * 2f, 0.12f, HallZ * 2f), floor, 0f, default, true);

        // paredes: fundo em alvenaria, laterais em concreto
        Block(p, "WallBack", new Vector3(0, CeilY * 0.5f, -HallZ - 0.15f), new Vector3(HallX * 2f, CeilY, 0.3f), brick, 0.03f, default, true);
        Block(p, "WallLeft", new Vector3(-HallX - 0.15f, CeilY * 0.5f, 0), new Vector3(0.3f, CeilY, HallZ * 2f), concrete, 0.03f, default, true);
        Block(p, "WallRight", new Vector3(HallX + 0.15f, CeilY * 0.5f, 0), new Vector3(0.3f, CeilY, HallZ * 2f), concrete, 0.03f, default, true);
        Block(p, "Ceiling", new Vector3(0, CeilY + 0.12f, 0), new Vector3(HallX * 2f, 0.24f, HallZ * 2f), ceiling, 0f);

        // pilares entre os panos de alvenaria
        for (int i = -1; i <= 1; i++)
            Block(p, "PillarB" + (i + 2), new Vector3(i * 7.5f, CeilY * 0.5f, -HallZ + 0.45f), new Vector3(0.72f, CeilY, 0.72f), concrete, 0.035f);
        for (int i = -1; i <= 1; i++)
        {
            Block(p, "PillarL" + (i + 2), new Vector3(-HallX + 0.45f, CeilY * 0.5f, i * 6f), new Vector3(0.72f, CeilY, 0.72f), concrete, 0.035f);
            Block(p, "PillarR" + (i + 2), new Vector3(HallX - 0.45f, CeilY * 0.5f, i * 6f), new Vector3(0.72f, CeilY, 0.72f), concrete, 0.035f);
        }

        // vigas metalicas do teto, como na referencia
        for (int i = -3; i <= 3; i++)
            Block(p, "TrussZ" + (i + 4), new Vector3(i * 3.6f, CeilY - 0.32f, 0), new Vector3(0.26f, 0.48f, HallZ * 2f), steel, 0.02f);
        for (int i = -1; i <= 1; i++)
            Block(p, "TrussX" + (i + 2), new Vector3(0, CeilY - 0.68f, i * 5.5f), new Vector3(HallX * 2f, 0.28f, 0.22f), steel, 0.02f);

        // janelas altas no fundo, como em image2
        var glass = Mat("GalpaoGlass", "Standard", new Color(0.09f, 0.10f, 0.12f), 0.85f, 0.1f);
        for (int i = -2; i <= 2; i++)
            Block(p, "Window" + (i + 3), new Vector3(i * 3.0f, 4.4f, -HallZ + 0.2f), new Vector3(1.8f, 1.6f, 0.06f), glass, 0.01f);
    }

    // ---------------------------------------------------------------- iluminacao

    static void BuildLighting(Transform p)
    {
        var bulb = Mat("GalpaoBulb", "TwentyOne/PracticalGlow", new Color(1f, 0.84f, 0.58f));
        var shaft = Mat("GalpaoShaft", "TwentyOne/LightShaft", Warm);
        shaft.SetFloat("_Intensity", 0.06f);
        EditorUtility.SetDirty(shaft);
        var shade = Mat("GalpaoShade", "Standard", new Color(0.070f, 0.070f, 0.075f), 0.30f, 0.60f);

        // grade 4 x 2 de pendentes, como nas fotos
        int n = 0;
        for (int ix = 0; ix < 4; ix++)
        {
            for (int iz = 0; iz < 2; iz++)
            {
                n++;
                var lamp = Group(p, "Lamp" + n.ToString("00"));
                lamp.localPosition = new Vector3(-6.75f + ix * 4.5f, 0, -3.4f + iz * 6.8f);

                Cylinder(lamp, "Rod", new Vector3(0, 5.6f, 0), 0.025f, 1.7f, 0, shade);
                Cylinder(lamp, "Shade", new Vector3(0, 4.62f, 0), 0.34f, 0.26f, 0, shade);

                // facho visivel. GenerateCone ja deixa o vertice em +Y, que e
                // exatamente o que o facho precisa: ponta na lampada, base no
                // chao. Nao girar, senao o cone fica de cabeca para baixo.
                var cone = ShapeGenerator.GenerateCone(PivotLocation.Center, 0.78f, 4.2f, 16);
                cone.name = "Shaft";
                cone.transform.SetParent(lamp, false);
                cone.transform.localPosition = new Vector3(0, 2.4f, 0);
                Skin(cone, shaft);
                cone.gameObject.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

                var glow = ShapeGenerator.GeneratePlane(PivotLocation.Center, 0.62f, 0.62f, 1, 1, Axis.Forward);
                glow.name = "Glow";
                glow.transform.SetParent(lamp, false);
                glow.transform.localPosition = new Vector3(0, 4.42f, 0);
                Skin(glow, bulb);
                glow.gameObject.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

                var lightGo = new GameObject("Light");
                lightGo.transform.SetParent(lamp, false);
                lightGo.transform.localPosition = new Vector3(0, 4.35f, 0);
                var lt = lightGo.AddComponent<Light>();
                lt.type = LightType.Point;
                // Branco quente, nao alaranjado: com range curto o piso de
                // concreto ainda precisa ler como cinza, como nas fotos.
                lt.color = new Color(1f, 0.91f, 0.79f);
                lt.intensity = 2.2f;
                lt.range = 9f;
                lt.shadows = LightShadows.None;
                lt.renderMode = LightRenderMode.ForcePixel;
            }
        }
    }

    static ProBuilderMesh Cylinder(Transform parent, string name, Vector3 pos, float radius, float height, int cuts, Material mat)
    {
        var mesh = ShapeGenerator.GenerateCylinder(PivotLocation.Center, 12, radius, height, cuts, 0);
        mesh.name = name;
        mesh.transform.SetParent(parent, false);
        mesh.transform.localPosition = pos;
        Skin(mesh, mat);
        return mesh;
    }

    // ---------------------------------------------------------------------- shop

    static void BuildShop(Transform p)
    {
        var wood = Mat("GalpaoWood", "Standard", new Color(0.100f, 0.064f, 0.038f), 0.24f);
        var woodTop = Mat("GalpaoWoodLight", "Standard", new Color(0.140f, 0.092f, 0.055f), 0.30f);
        var felt = Felt("GalpaoFelt", new Color(0.052f, 0.180f, 0.082f));
        var steel = Mat("GalpaoSteel", "Standard", new Color(0.095f, 0.095f, 0.105f), 0.55f, 0.85f);
        var panel = Mat("GalpaoSignPanel", "Standard", new Color(0.030f, 0.032f, 0.035f), 0.35f, 0.30f);
        var neon = Mat("GalpaoNeon", "TwentyOne/PracticalGlow", Mint);
        var crate = Mat("GalpaoCrate", "Standard", new Color(0.115f, 0.080f, 0.050f), 0.18f);

        // balcao no centro do salao
        var counter = Group(p, "Counter");
        Block(counter, "Top", new Vector3(0, 1.05f, 0), new Vector3(3.6f, 0.09f, 1.5f), felt, 0.012f);
        Block(counter, "Front", new Vector3(0, 0.52f, -0.71f), new Vector3(3.6f, 1.05f, 0.12f), wood, 0.022f);
        Block(counter, "Back", new Vector3(0, 0.52f, 0.71f), new Vector3(3.6f, 1.05f, 0.12f), wood, 0.022f);
        Block(counter, "SideL", new Vector3(-1.74f, 0.52f, 0), new Vector3(0.12f, 1.05f, 1.5f), wood, 0.022f);
        Block(counter, "SideR", new Vector3(1.74f, 0.52f, 0), new Vector3(0.12f, 1.05f, 1.5f), wood, 0.022f);
        // vao arqueado sob o balcao, como na foto
        Block(counter, "Kick", new Vector3(0, 0.09f, 0), new Vector3(3.0f, 0.18f, 1.1f), woodTop, 0.02f);
        Block(counter, "Ledge", new Vector3(0, 1.16f, 0.68f), new Vector3(3.7f, 0.07f, 0.24f), woodTop, 0.014f);

        // engradados em cima do balcao
        Block(counter, "CrateA", new Vector3(-1.05f, 1.25f, -0.10f), new Vector3(0.50f, 0.40f, 0.50f), crate, 0.03f, new Vector3(0, 14f, 0));
        Block(counter, "CrateB", new Vector3(-0.48f, 1.21f, 0.20f), new Vector3(0.42f, 0.34f, 0.42f), crate, 0.03f, new Vector3(0, -8f, 0));
        Block(counter, "CrateC", new Vector3(1.15f, 1.23f, 0.15f), new Vector3(0.46f, 0.36f, 0.46f), crate, 0.03f, new Vector3(0, 6f, 0));

        // hastes da placa
        for (int i = -1; i <= 1; i += 2)
            Cylinder(p, "SignRod" + (i + 2), new Vector3(i * 1.3f, 5.0f, 0), 0.03f, 1.6f, 0, steel);

        // placa SHOP suspensa
        var sign = Group(p, "Sign");
        Block(sign, "Frame", new Vector3(0, 4.05f, 0), new Vector3(3.4f, 1.0f, 0.14f), panel, 0.02f);
        Block(sign, "FrameTop", new Vector3(0, 4.58f, 0), new Vector3(3.6f, 0.11f, 0.20f), steel, 0.02f);
        Block(sign, "FrameBot", new Vector3(0, 3.52f, 0), new Vector3(3.6f, 0.11f, 0.20f), steel, 0.02f);
        ShopText(sign.transform, "SHOP", new Vector3(0, 4.05f, 0.10f), 150, Mint);

        // marquise: piramide de 4 lados, com a base quadrada made a medida
        var canopy = Group(p, "Canopy");
        var pyr = ShapeGenerator.GenerateCone(PivotLocation.Center, 1.45f, 1.5f, 4);
        pyr.name = "Canopy";
        pyr.transform.SetParent(canopy, false);
        pyr.transform.localPosition = new Vector3(0, 2.55f, 0);
        pyr.transform.localEulerAngles = new Vector3(0, 45f, 0);
        Skin(pyr, panel);
        Bevel(pyr, 0.03f);
        // borda neon: 4 vigas nas arestas da piramide
        for (int i = 0; i < 4; i++)
        {
            var e = Block(canopy, "Edge" + i, Vector3.zero, new Vector3(0.055f, 0.055f, 2.05f), neon, 0f, new Vector3(35f, i * 90f, 0));
            e.transform.localPosition = new Vector3(0, 2.62f, 0);
            e.gameObject.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    // Texto do SHOP em canvas no mundo, como o IntroMenuVRChat faz.
    static void ShopText(Transform parent, string text, Vector3 pos, int size, Color color)
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        if (font == null) { Debug.LogWarning("GalpaoBuilder: fonte nao encontrada em " + FontPath); return; }

        var go = new GameObject("Text_" + text);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        // 180 graus em Y: o canvas no mundo de Unity escreve o texto pelo lado
        // -Z, entao sem esta rotacao ele chega espelhado para quem entra.
        go.transform.localEulerAngles = new Vector3(0f, 180f, 0f);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(1400, 420);
        rect.localScale = Vector3.one * 0.0021f;

        var label = go.AddComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = color;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
    }

    // -------------------------------------------------------------------- mesas

    static void BuildTables(Transform p)
    {
        var wood = Mat("GalpaoWood", "Standard", new Color(0.100f, 0.064f, 0.038f), 0.24f);
        var felt = Felt("GalpaoFelt", new Color(0.052f, 0.180f, 0.082f));

        int n = 0;
        for (int ix = 0; ix < 2; ix++)
        {
            for (int iz = 0; iz < 2; iz++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    n++;
                    var t = Group(p, "Table" + n.ToString("00"));
                    t.localPosition = new Vector3(side * (5.6f + ix * 4.0f), 0, -3.6f + iz * 7.2f);
                    Table(t, wood, felt);
                }
            }
        }
    }

    static void Table(Transform t, Material wood, Material felt)
    {
        // tampo de feltro rebaixado dentro do caixote de madeira
        Block(t, "Felt", new Vector3(0, 0.775f, 0), new Vector3(2.00f, 0.03f, 1.20f), felt, 0f);
        Block(t, "RailL", new Vector3(-1.06f, 0.74f, 0), new Vector3(0.13f, 0.13f, 1.44f), wood, 0.022f);
        Block(t, "RailR", new Vector3(1.06f, 0.74f, 0), new Vector3(0.13f, 0.13f, 1.44f), wood, 0.022f);
        Block(t, "RailF", new Vector3(0, 0.74f, -0.655f), new Vector3(2.25f, 0.13f, 0.13f), wood, 0.022f);
        Block(t, "RailB", new Vector3(0, 0.74f, 0.655f), new Vector3(2.25f, 0.13f, 0.13f), wood, 0.022f);
        // pe-de-mesa em cruz, como nas fotos
        Block(t, "LegL", new Vector3(-0.82f, 0.36f, 0), new Vector3(0.15f, 0.72f, 1.10f), wood, 0.018f);
        Block(t, "LegR", new Vector3(0.82f, 0.36f, 0), new Vector3(0.15f, 0.72f, 1.10f), wood, 0.018f);
        Block(t, "Stretch", new Vector3(0, 0.18f, 0), new Vector3(1.70f, 0.09f, 0.13f), wood, 0.016f);

        // quatro cadeiras, duas por lado longo
        for (int i = -1; i <= 1; i += 2)
        {
            for (int j = -1; j <= 1; j += 2)
            {
                var c = Group(t, "Chair" + i + j);
                c.localPosition = new Vector3(i * 1.45f, 0, j * 0.80f);
                c.localEulerAngles = new Vector3(0, j > 0 ? 180f : 0f, 0);
                Block(c, "Seat", new Vector3(0, 0.45f, 0), new Vector3(0.44f, 0.07f, 0.44f), wood, 0.02f);
                Block(c, "Back", new Vector3(0, 0.76f, 0.20f), new Vector3(0.44f, 0.56f, 0.07f), wood, 0.02f);
                Block(c, "Base", new Vector3(0, 0.21f, 0), new Vector3(0.38f, 0.42f, 0.38f), wood, 0.02f);
            }
        }
    }

    // ---------------------------------------------------------------- placares

    static void BuildBoards(Transform p)
    {
        var board = Mat("GalpaoBoard", "Standard", new Color(0.022f, 0.024f, 0.028f), 0.30f, 0.20f);
        var row = Mat("GalpaoBoardRow", "Standard", new Color(0.30f, 0.31f, 0.33f), 0.20f);
        var frame = Mat("GalpaoBoardFrame", "Standard", new Color(0.075f, 0.075f, 0.080f), 0.45f, 0.70f);

        // dois placares altos em cada parede lateral
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 2; i++)
            {
                var b = Group(p, "Board" + (side > 0 ? "R" : "L") + i);
                b.localPosition = new Vector3(side * (HallX - 0.22f), 2.5f, -3.6f + i * 7.2f);
                b.localEulerAngles = new Vector3(0, side > 0 ? -90f : 90f, 0);

                Block(b, "Panel", Vector3.zero, new Vector3(3.2f, 2.2f, 0.08f), board, 0.012f);
                Block(b, "FrameT", new Vector3(0, 1.14f, 0), new Vector3(3.32f, 0.13f, 0.13f), frame, 0.015f);
                Block(b, "FrameB", new Vector3(0, -1.14f, 0), new Vector3(3.32f, 0.13f, 0.13f), frame, 0.015f);
                for (int r = 0; r < 8; r++)
                    Block(b, "Row" + r, new Vector3(0, 0.82f - r * 0.22f, 0.07f), new Vector3(2.9f, 0.12f, 0.02f), row, 0f);
            }
        }

        // placar de pe no fundo, como em image3
        for (int i = -1; i <= 1; i += 2)
        {
            var s = Group(p, "Stand" + (i + 2));
            s.localPosition = new Vector3(i * 4.4f, 0, -7.6f);
            Block(s, "Panel", new Vector3(0, 1.75f, 0), new Vector3(2.6f, 1.9f, 0.08f), board, 0.012f);
            Block(s, "Frame", new Vector3(0, 1.75f, 0.05f), new Vector3(2.72f, 2.02f, 0.04f), frame, 0.012f);
            for (int r = 0; r < 7; r++)
                Block(s, "Row" + r, new Vector3(0, 2.45f - r * 0.23f, 0.07f), new Vector3(2.3f, 0.11f, 0.02f), row, 0f);
            Block(s, "LegL", new Vector3(-1.0f, 0.45f, 0.1f), new Vector3(0.09f, 0.9f, 0.09f), frame, 0.015f, new Vector3(0, 0, 12f));
            Block(s, "LegR", new Vector3(1.0f, 0.45f, 0.1f), new Vector3(0.09f, 0.9f, 0.09f), frame, 0.015f, new Vector3(0, 0, -12f));
        }
    }

    // ------------------------------------------------------------------ cenario

    static void BuildProps(Transform p)
    {
        var crate = Mat("GalpaoCrate", "Standard", new Color(0.115f, 0.080f, 0.050f), 0.18f);
        var steel = Mat("GalpaoSteel", "Standard", new Color(0.095f, 0.095f, 0.105f), 0.55f, 0.85f);
        var wood = Mat("GalpaoWood", "Standard", new Color(0.100f, 0.064f, 0.038f), 0.24f);
        var felt = Felt("GalpaoFelt", new Color(0.052f, 0.180f, 0.082f));
        var yellow = Mat("GalpaoPosterY", "Standard", new Color(0.42f, 0.36f, 0.10f), 0.15f);
        var orange = Mat("GalpaoPosterO", "Standard", new Color(0.44f, 0.19f, 0.07f), 0.15f);
        var red = Mat("GalpaoPosterR", "Standard", new Color(0.36f, 0.07f, 0.08f), 0.15f);

        // pilhas de engradados nos cantos
        Vector3[] crates = {
            new Vector3(-11.4f, 0.31f, -7.4f), new Vector3(-11.0f, 0.91f, -7.2f),
            new Vector3(11.4f, 0.31f, -7.4f), new Vector3(11.6f, 0.91f, -6.8f),
            new Vector3(-11.6f, 0.31f, 7.4f), new Vector3(11.2f, 0.31f, 7.6f),
            new Vector3(11.5f, 0.91f, 7.2f)
        };
        for (int i = 0; i < crates.Length; i++)
            Block(p, "Crate" + i, crates[i], new Vector3(0.9f, 0.6f, 0.9f), crate, 0.03f, new Vector3(0, i * 17f, 0));

        // tambores
        Cylinder(p, "DrumA", new Vector3(-12.2f, 0.45f, 2.2f), 0.3f, 0.9f, 1, steel);
        Cylinder(p, "DrumB", new Vector3(-11.6f, 0.45f, 3.0f), 0.3f, 0.9f, 1, steel);
        Cylinder(p, "DrumC", new Vector3(12.2f, 0.45f, 1.4f), 0.3f, 0.9f, 1, steel);

        // mesa de sinuca, visivel em image3 e image4
        var pool = Group(p, "PoolTable");
        pool.localPosition = new Vector3(-9.8f, 0, 7.2f);
        pool.localEulerAngles = new Vector3(0, 12f, 0);
        Block(pool, "Felt", new Vector3(0, 0.80f, 0), new Vector3(2.2f, 0.04f, 1.1f), felt, 0f);
        Block(pool, "RailL", new Vector3(-1.15f, 0.76f, 0), new Vector3(0.13f, 0.15f, 1.34f), wood, 0.022f);
        Block(pool, "RailR", new Vector3(1.15f, 0.76f, 0), new Vector3(0.13f, 0.15f, 1.34f), wood, 0.022f);
        Block(pool, "RailF", new Vector3(0, 0.76f, -0.6f), new Vector3(2.42f, 0.15f, 0.13f), wood, 0.022f);
        Block(pool, "RailB", new Vector3(0, 0.76f, 0.6f), new Vector3(2.42f, 0.15f, 0.13f), wood, 0.022f);
        Block(pool, "BodyL", new Vector3(-0.8f, 0.38f, 0), new Vector3(0.17f, 0.76f, 0.9f), wood, 0.02f);
        Block(pool, "BodyR", new Vector3(0.8f, 0.38f, 0), new Vector3(0.17f, 0.76f, 0.9f), wood, 0.02f);
        Block(pool, "BodyF", new Vector3(0, 0.38f, 0), new Vector3(1.6f, 0.76f, 0.9f), wood, 0.02f);

        // cartazes nas paredes, como em image3
        var back = Group(p, "Posters");
        back.localPosition = new Vector3(0, 0, -HallZ + 0.2f);
        for (int i = 0; i < 3; i++)
        {
            var q = ShapeGenerator.GeneratePlane(PivotLocation.Center, 1.8f, 1.2f, 1, 1, Axis.Forward);
            q.name = "Poster" + i;
            q.transform.SetParent(back, false);
            q.transform.localPosition = new Vector3(-6.0f + i * 2.4f, 2.4f, 0);
            q.transform.localEulerAngles = new Vector3(0, 180f, 0);
            Skin(q, i == 0 ? yellow : i == 1 ? orange : red);
        }
    }

    // ------------------------------------------------------------------ cena

    // Espelha as configuracoes do GameMap: flat ambient quase preto,
    // sem skybox e reflexao baixa, para o Galpao nao destoar do mapa.
    static void ApplyRenderSettings()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.045f, 0.047f, 0.050f);
        RenderSettings.skybox = null;
        RenderSettings.reflectionIntensity = 0.12f;
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
    }
}
