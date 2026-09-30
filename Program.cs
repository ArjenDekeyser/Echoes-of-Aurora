using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Raylib_cs;
using static Raylib_cs.Raylib;

public static class Program
{
    public static void Main()
    {
        var game = new AuroraGame();
        game.Run();
    }
}

internal sealed class AuroraGame
{
    private const int ScreenWidth = 1280;
    private const int ScreenHeight = 720;
    private const float WorldHalf = 80f;

    private Camera3D camera;
    private readonly List<ResourceNode> resources = new();
    private readonly List<Structure> structures = new();
    private readonly List<Tree> trees = new();
    private readonly List<Npc> npcs = new();
    private readonly List<Enemy> enemies = new();
    private Model playerModel;
    private Model npcModel;
    private Model enemyModel;
    private bool modelAssetsLoaded;
    private Sound collectSound;
    private Sound attackSound;
    private Sound victorySound;
    private bool audioReady;

    private readonly Dictionary<string, int> inventory = new()
    {
        ["wood"] = 0,
        ["stone"] = 0,
        ["fiber"] = 0,
        ["crystal"] = 0,
        ["water"] = 0,
        ["berries"] = 0,
        ["copper"] = 0,
        ["iron"] = 0,
        ["auroraShard"] = 0,
        ["basicTool"] = 0,
        ["scanner"] = 0,
        ["shelter"] = 0,
    };

    private readonly Player player = new()
    {
        Position = new Vector3(0f, 1.4f, 16f),
        Speed = 12f,
        Radius = 0.8f,
        Health = 100,
        Stamina = 100,
        Hunger = 100,
        Thirst = 100,
    };

    private float dayTime;
    private float cameraYaw = 0.8f;
    private float cameraPitch = 0.4f;
    private float messageTimer;
    private float questFlash;
    private float animationTime;
    private int questStage;
    private bool buildMode;
    private bool gameWon;
    private bool showMap;
    private bool showJournal;
    private float attackCooldown;
    private string statusText = "Wake up. Aurora is listening.";
    private string objectiveText = "Find enough wood, fiber and stone to repair the capsule.";
    private const string SaveFileName = "aurora-save.json";

    public void Run()
    {
        InitWindow(ScreenWidth, ScreenHeight, "Echoes of Aurora");
        SetTargetFPS(60);

        camera = new Camera3D();
        InitializeAudio();
        LoadAssetModels();
        PrepareWorld();
        ResetCamera();

        while (!WindowShouldClose())
        {
            Update();
            Draw();
        }

        if (modelAssetsLoaded)
        {
            UnloadModel(playerModel);
            UnloadModel(npcModel);
            UnloadModel(enemyModel);
        }

        if (audioReady)
        {
            UnloadSound(collectSound);
            UnloadSound(attackSound);
            UnloadSound(victorySound);
            CloseAudioDevice();
        }

        CloseWindow();
    }

    private void LoadAssetModels()
    {
        var assetRoot = Path.Combine(AppContext.BaseDirectory, "assets");
        var playerPath = Path.Combine(assetRoot, "player.obj");
        var npcPath = Path.Combine(assetRoot, "aurora_npc.obj");
        var enemyPath = Path.Combine(assetRoot, "gloom_stalker.obj");
        if (!File.Exists(playerPath) || !File.Exists(npcPath) || !File.Exists(enemyPath)) return;

        playerModel = LoadModel(playerPath);
        npcModel = LoadModel(npcPath);
        enemyModel = LoadModel(enemyPath);
        modelAssetsLoaded = true;
    }

    private void InitializeAudio()
    {
        InitAudioDevice();
        if (!IsAudioDeviceReady()) return;

        collectSound = CreateTone(620f, 0.12f, 0.22f);
        attackSound = CreateTone(145f, 0.08f, 0.18f);
        victorySound = CreateTone(880f, 0.45f, 0.24f);
        audioReady = true;
    }

    private static unsafe Sound CreateTone(float frequency, float duration, float volume)
    {
        const int sampleRate = 44100;
        var sampleCount = (int)(sampleRate * duration);
        var samples = new short[sampleCount];
        for (var index = 0; index < sampleCount; index++)
        {
            var envelope = 1f - index / (float)sampleCount;
            samples[index] = (short)(MathF.Sin(index / (float)sampleRate * MathF.Tau * frequency) * short.MaxValue * volume * envelope);
        }

        var buffer = Marshal.AllocHGlobal(samples.Length * sizeof(short));
        Marshal.Copy(samples, 0, buffer, samples.Length);
        var wave = new Wave
        {
            FrameCount = (uint)sampleCount,
            SampleRate = sampleRate,
            SampleSize = 16,
            Channels = 1,
            Data = (void*)buffer,
        };
        var sound = LoadSoundFromWave(wave);
        UnloadWave(wave);
        return sound;
    }

    private void PlayEffect(Sound sound)
    {
        if (audioReady && IsSoundValid(sound)) PlaySound(sound);
    }

    private void PrepareWorld()
    {
        trees.Add(new Tree(new Vector3(-12f, 0f, -8f), 2.8f));
        trees.Add(new Tree(new Vector3(-8f, 0f, 7f), 3.4f));
        trees.Add(new Tree(new Vector3(14f, 0f, -16f), 3.2f));
        trees.Add(new Tree(new Vector3(18f, 0f, 12f), 3.6f));
        trees.Add(new Tree(new Vector3(-22f, 0f, 18f), 3.3f));

        resources.Add(new ResourceNode("wood", new Vector3(-20f, 1f, 14f), new Color((byte)104, (byte)151, (byte)90, (byte)255)));
        resources.Add(new ResourceNode("wood", new Vector3(-14f, 1f, 6f), new Color((byte)104, (byte)151, (byte)90, (byte)255)));
        resources.Add(new ResourceNode("stone", new Vector3(-4f, 1f, 17f), new Color((byte)145, (byte)163, (byte)176, (byte)255)));
        resources.Add(new ResourceNode("fiber", new Vector3(16f, 1f, 20f), new Color((byte)111, (byte)204, (byte)126, (byte)255)));
        resources.Add(new ResourceNode("crystal", new Vector3(22f, 1f, 11f), new Color((byte)130, (byte)228, (byte)255, (byte)255)));
        resources.Add(new ResourceNode("copper", new Vector3(28f, 1f, -18f), new Color((byte)208, (byte)140, (byte)90, (byte)255)));
        resources.Add(new ResourceNode("water", new Vector3(-28f, 1f, -12f), new Color((byte)89, (byte)194, (byte)255, (byte)255)));
        resources.Add(new ResourceNode("berries", new Vector3(8f, 1f, -12f), new Color((byte)255, (byte)118, (byte)160, (byte)255)));
        resources.Add(new ResourceNode("crystal", new Vector3(48f, 1f, -28f), new Color((byte)130, (byte)228, (byte)255, (byte)255)));
        resources.Add(new ResourceNode("iron", new Vector3(-52f, 1f, -24f), new Color((byte)180, (byte)185, (byte)195, (byte)255)));
        resources.Add(new ResourceNode("berries", new Vector3(-42f, 1f, 38f), new Color((byte)255, (byte)118, (byte)160, (byte)255)));

        npcs.Add(new Npc("Mira", new Vector3(10f, 1.3f, -4f), new Color((byte)125, (byte)220, (byte)255, (byte)255)));
        npcs.Add(new Npc("Pip", new Vector3(0f, 1.3f, 12f), new Color((byte)170, (byte)255, (byte)220, (byte)255)));
        npcs.Add(new Npc("Orin", new Vector3(-34f, 1.3f, -28f), new Color((byte)255, (byte)192, (byte)120, (byte)255)));

        enemies.Add(new Enemy("Gloom Stalker", new Vector3(24f, 1.2f, -8f), 48f));
        enemies.Add(new Enemy("Gloom Stalker", new Vector3(-34f, 1.2f, 26f), 48f));
        enemies.Add(new Enemy("Gloom Stalker", new Vector3(42f, 1.2f, 36f), 48f));
        enemies.Add(new Enemy("Gloom Stalker", new Vector3(-48f, 1.2f, -36f), 72f));

        structures.Add(new Structure(new Vector3(-18f, 0.5f, 4f), new Vector3(2.8f, 1.0f, 2.8f), new Color((byte)102, (byte)82, (byte)68, (byte)255), "camp"));
        structures.Add(new Structure(new Vector3(-2f, 0.5f, 17f), new Vector3(2.5f, 1.8f, 2.5f), new Color((byte)90, (byte)125, (byte)94, (byte)255), "capsule"));
        structures.Add(new Structure(new Vector3(34f, 0.5f, 20f), new Vector3(3f, 1.2f, 3f), new Color((byte)93, (byte)133, (byte)180, (byte)255), "signal"));
    }

    private void ResetCamera()
    {
        camera.Position = player.Position + new Vector3(
            MathF.Sin(cameraYaw) * 8f,
            4.5f + cameraPitch * 2.5f,
            MathF.Cos(cameraYaw) * 8f);
        camera.Target = player.Position + new Vector3(0f, 1.1f, 0f);
        camera.Up = new Vector3(0f, 1f, 0f);
        camera.FovY = 65f;
        camera.Projection = CameraProjection.Perspective;
    }

    private void Update()
    {
        var dt = GetFrameTime();
        animationTime += dt;
        dayTime = (dayTime + dt * 0.025f) % 1f;
        messageTimer = MathF.Max(0f, messageTimer - dt);
        questFlash = MathF.Max(0f, questFlash - dt * 0.8f);

        HandleMovement(dt);
        HandleInteractions();
        HandleCombat(dt);
        UpdateEnemies(dt);
        UpdateSurvival(dt);
        HandleMenus();
        UpdateCamera();
        UpdateObjective();
    }

    private void HandleMovement(float dt)
    {
        var move = Vector3.Zero;

        if (IsKeyDown(KeyboardKey.W)) move += new Vector3(0f, 0f, -1f);
        if (IsKeyDown(KeyboardKey.S)) move += new Vector3(0f, 0f, 1f);
        if (IsKeyDown(KeyboardKey.A)) move += new Vector3(-1f, 0f, 0f);
        if (IsKeyDown(KeyboardKey.D)) move += new Vector3(1f, 0f, 0f);

        if (move.LengthSquared() > 0.01f)
        {
            move = Vector3.Normalize(move);
            var turn = Quaternion.CreateFromYawPitchRoll(cameraYaw, 0f, 0f);
            var rotated = Vector3.Transform(move, turn);
            var sprinting = IsKeyDown(KeyboardKey.LeftShift) && player.Stamina > 1f;
            var speed = sprinting ? 18f : 12f;
            player.Position += rotated * speed * dt;
            player.Stamina = MathF.Max(0f, player.Stamina - (sprinting ? 24f : 6f) * dt);
        }
        else
        {
            player.Stamina = MathF.Min(100f, player.Stamina + 18f * dt);
        }

        player.Position = new Vector3(
            Math.Clamp(player.Position.X, -WorldHalf, WorldHalf),
            1.4f,
            Math.Clamp(player.Position.Z, -WorldHalf, WorldHalf));

        if (IsMouseButtonDown(MouseButton.Right))
        {
            var delta = GetMouseDelta();
            cameraYaw -= delta.X * 0.008f;
            cameraPitch = Math.Clamp(cameraPitch - delta.Y * 0.005f, -0.3f, 1.0f);
        }

        if (IsKeyPressed(KeyboardKey.B))
        {
            buildMode = !buildMode;
            statusText = buildMode ? "Build mode active. Press E to place a shelter at your feet." : "Build mode deactivated.";
        }

        if (buildMode && IsKeyPressed(KeyboardKey.E))
        {
            if (inventory["wood"] >= 2 && inventory["stone"] >= 1)
            {
                inventory["wood"] -= 2;
                inventory["stone"] -= 1;
                inventory["shelter"] += 1;
                structures.Add(new Structure(player.Position + new Vector3(0f, 0.5f, 0f), new Vector3(2.5f, 1.8f, 2.5f), new Color((byte)162, (byte)196, (byte)174, (byte)255), "camp"));
                statusText = "A small shelter is now standing by the camp.";
                objectiveText = "The camp is stable. Seek Mira and continue the story.";
                questStage = Math.Max(questStage, 2);
            }
            else
            {
                statusText = "Need 2 wood and 1 stone to build a shelter.";
            }
        }

        if (IsKeyPressed(KeyboardKey.One)) CraftRecipe("basicTool");
        if (IsKeyPressed(KeyboardKey.Two)) CraftRecipe("scanner");
        if (IsKeyPressed(KeyboardKey.Three)) CraftRecipe("shelter");
        if (IsKeyPressed(KeyboardKey.Four)) ConsumeItem("berries");
        if (IsKeyPressed(KeyboardKey.Five)) ConsumeItem("water");
    }

    private void ConsumeItem(string item)
    {
        if (inventory.GetValueOrDefault(item, 0) <= 0)
        {
            statusText = $"No {item} available.";
            return;
        }

        inventory[item] -= 1;
        if (item == "berries")
        {
            player.Hunger = MathF.Min(100f, player.Hunger + 28f);
            statusText = "You eat Aurora berries. Hunger restored.";
        }
        else
        {
            player.Thirst = MathF.Min(100f, player.Thirst + 35f);
            statusText = "You drink clean water. Thirst restored.";
        }
    }

    private void UpdateSurvival(float dt)
    {
        player.Hunger = MathF.Max(0f, player.Hunger - dt * 0.8f);
        player.Thirst = MathF.Max(0f, player.Thirst - dt * 1.2f);
        if (player.Hunger <= 0f || player.Thirst <= 0f)
        {
            player.Health = MathF.Max(0f, player.Health - dt * 5f);
            statusText = "Survival warning: find berries or water.";
        }
    }

    private void HandleMenus()
    {
        if (IsKeyPressed(KeyboardKey.M))
        {
            showMap = !showMap;
            showJournal = false;
        }

        if (IsKeyPressed(KeyboardKey.J))
        {
            showJournal = !showJournal;
            showMap = false;
        }

        if (IsKeyPressed(KeyboardKey.F5)) SaveGame();
        if (IsKeyPressed(KeyboardKey.F9)) LoadGame();
    }

    private void HandleCombat(float dt)
    {
        attackCooldown = MathF.Max(0f, attackCooldown - dt);
        if ((!IsMouseButtonPressed(MouseButton.Left) && !IsKeyPressed(KeyboardKey.Space)) || attackCooldown > 0f)
        {
            return;
        }

        if (inventory.GetValueOrDefault("basicTool", 0) == 0)
        {
            statusText = "Craft the basic tool before fighting.";
            return;
        }

        var target = enemies
            .OrderBy(enemy => Vector3.Distance(enemy.Position, player.Position))
            .FirstOrDefault(enemy => Vector3.Distance(enemy.Position, player.Position) < 3.6f);

        if (target is null)
        {
            statusText = "Nothing is within striking distance.";
            return;
        }

        target.Health -= 32f;
        attackCooldown = 0.45f;
        PlayEffect(attackSound);
        statusText = $"You strike the {target.Name}.";
        if (target.Health <= 0f)
        {
            enemies.Remove(target);
            inventory["iron"] += 1;
            PlayEffect(collectSound);
            statusText = $"The {target.Name} fades into crystal dust. +1 iron.";
        }
    }

    private void UpdateEnemies(float dt)
    {
        var dangerActive = dayTime > 0.55f || questStage >= 2;
        foreach (var enemy in enemies)
        {
            enemy.PatrolTime += dt * 0.65f;
            if (!dangerActive)
            {
                enemy.Position = enemy.Anchor + new Vector3(MathF.Cos(enemy.PatrolTime) * 4f, 0f, MathF.Sin(enemy.PatrolTime) * 4f);
                continue;
            }

            var toPlayer = player.Position - enemy.Position;
            var distance = toPlayer.Length();
            if (distance > 24f)
            {
                var patrolTarget = enemy.Anchor + new Vector3(MathF.Cos(enemy.PatrolTime) * 5f, 0f, MathF.Sin(enemy.PatrolTime) * 5f);
                var patrolDirection = patrolTarget - enemy.Position;
                if (patrolDirection.LengthSquared() > 0.1f)
                {
                    enemy.Position += Vector3.Normalize(patrolDirection) * enemy.Speed * 0.35f * dt;
                }
                continue;
            }

            if (distance > 1.7f)
            {
                enemy.Position += Vector3.Normalize(toPlayer) * enemy.Speed * dt;
            }
            else
            {
                enemy.DamageCooldown = MathF.Max(0f, enemy.DamageCooldown - dt);
                if (enemy.DamageCooldown <= 0f)
                {
                    player.Health = MathF.Max(0f, player.Health - enemy.Damage);
                    enemy.DamageCooldown = 1.2f;
                    statusText = "A Gloom Stalker struck you.";
                }
            }
        }

        if (player.Health > 0f) return;

        player.Position = new Vector3(0f, 1.4f, 16f);
        player.Health = 100f;
        player.Stamina = 100f;
        statusText = "You wake at the capsule. The night remembers your defeat.";
    }

    private void SaveGame()
    {
        var save = new SaveData
        {
            PlayerPosition = player.Position,
            Health = player.Health,
            Stamina = player.Stamina,
            Hunger = player.Hunger,
            Thirst = player.Thirst,
            QuestStage = questStage,
            GameWon = gameWon,
            Inventory = new Dictionary<string, int>(inventory),
            CollectedResources = resources.Select(resource => resource.Type + "|" + resource.Position.X + "|" + resource.Position.Z).ToHashSet()
        };

        File.WriteAllText(SaveFileName, JsonSerializer.Serialize(save, new JsonSerializerOptions { WriteIndented = true }));
        statusText = "Game saved.";
    }

    private void LoadGame()
    {
        if (!File.Exists(SaveFileName))
        {
            statusText = "No save found yet. Press F5 to create one.";
            return;
        }

        var save = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(SaveFileName));
        if (save is null) return;

        player.Position = save.PlayerPosition;
        player.Health = save.Health;
        player.Stamina = save.Stamina;
        player.Hunger = save.Hunger;
        player.Thirst = save.Thirst;
        questStage = save.QuestStage;
        gameWon = save.GameWon;

        foreach (var key in inventory.Keys.ToList())
        {
            inventory[key] = save.Inventory.GetValueOrDefault(key, 0);
        }

        resources.RemoveAll(resource => save.CollectedResources.Contains(resource.Type + "|" + resource.Position.X + "|" + resource.Position.Z));
        statusText = "Game loaded.";
    }

    private void UpdateCamera()
    {
        var offset = new Vector3(
            MathF.Sin(cameraYaw) * MathF.Cos(cameraPitch) * 8f,
            4.8f + MathF.Sin(cameraPitch) * 6f,
            MathF.Cos(cameraYaw) * MathF.Cos(cameraPitch) * 8f);

        camera.Position = player.Position + offset;
        camera.Target = player.Position + new Vector3(0f, 1.3f, 0f);
        camera.Up = new Vector3(0f, 1f, 0f);
    }

    private void HandleInteractions()
    {
        var closestResource = resources
            .OrderBy(resource => Vector3.Distance(resource.Position, player.Position))
            .FirstOrDefault();

        if (closestResource is not null && Vector3.Distance(closestResource.Position, player.Position) < 2.8f && IsKeyPressed(KeyboardKey.E))
        {
            inventory[closestResource.Type] = inventory.GetValueOrDefault(closestResource.Type, 0) + 1;
            statusText = $"Collected {closestResource.Type}.";
            PlayEffect(collectSound);
            resources.Remove(closestResource);
            return;
        }

        var closestNpc = npcs
            .OrderBy(npc => Vector3.Distance(npc.Position, player.Position))
            .FirstOrDefault();

        if (closestNpc is not null && Vector3.Distance(closestNpc.Position, player.Position) < 3.2f && IsKeyPressed(KeyboardKey.E))
        {
            if (closestNpc.Name == "Mira")
            {
                if (questStage == 0)
                {
                    statusText = "Mira: The tower is not just a landmark. It is a memory vault.";
                    objectiveText = "Craft a scanner to reveal the Aurora signal.";
                    questStage = 1;
                }
                else if (questStage == 3 && inventory.GetValueOrDefault("auroraShard", 0) > 0)
                {
                    statusText = "Mira: The shard remembers the colony. Bring it to the first campfire.";
                    objectiveText = "Return to the first camp and place the Aurora Shard.";
                    questStage = 4;
                }
                else
                {
                    statusText = "Mira: The old colony stored its minds inside Aurora itself.";
                }
            }
            else if (closestNpc.Name == "Pip")
            {
                statusText = "Pip: The signal is growing louder. Aurora is waking up.";
            }
            else if (closestNpc.Name == "Orin")
            {
                statusText = "Orin: The northern trail is dangerous, but the old mines still hold iron.";
                if (questStage == 1)
                {
                    objectiveText = "Find crystal and copper to build the Aurora scanner.";
                }
            }
        }

        if (questStage == 0 && IsKeyPressed(KeyboardKey.E) && Vector3.Distance(new Vector3(-2f, 1.4f, 17f), player.Position) < 3.0f)
        {
            if (inventory["wood"] >= 3 && inventory["fiber"] >= 2 && inventory["stone"] >= 2)
            {
                statusText = "Capsule repaired. Pip awakens and the forest opens.";
                objectiveText = "Find Mira in the camp and learn what Aurora remembers.";
                questStage = 1;
                inventory["basicTool"] = 1;
            }
            else
            {
                statusText = "The capsule needs more wood, fiber and stone before it can restart.";
            }
        }

        if (questStage >= 1 && Vector3.Distance(new Vector3(34f, 1f, 20f), player.Position) < 4f && inventory.GetValueOrDefault("scanner", 0) > 0 && IsKeyPressed(KeyboardKey.E))
        {
            inventory["auroraShard"] += 1;
            statusText = "A hidden Aurora Shard sparks to life. The old signal is unlocked.";
            objectiveText = "Return to Mira and learn what the shard remembers.";
            questStage = 3;
        }

        if (questStage == 4 && Vector3.Distance(new Vector3(-18f, 1.4f, 4f), player.Position) < 4.5f && IsKeyPressed(KeyboardKey.E))
        {
            if (inventory.GetValueOrDefault("shelter", 0) > 0)
            {
                inventory["shelter"] -= 1;
                structures.Add(new Structure(new Vector3(-18f, 1.4f, 4f), new Vector3(3.6f, 2.4f, 3.6f), new Color((byte)118, (byte)220, (byte)195, (byte)255), "aurora campfire"));
                statusText = "The shard ignites the campfire. Aurora speaks through every memory.";
                objectiveText = "Aurora remembers. The story is awakening.";
                questStage = 5;
                gameWon = true;
                PlayEffect(victorySound);
            }
            else
            {
                statusText = "Build a shelter kit first, then use it to anchor the Aurora Shard.";
            }
        }
    }

    private void CraftRecipe(string recipeName)
    {
        if (recipeName == "basicTool")
        {
            if (TrySpend(new Dictionary<string, int> { ["wood"] = 3, ["fiber"] = 2 }))
            {
                inventory["basicTool"] += 1;
                statusText = "Basic tool crafted.";
            }
            else
            {
                statusText = "Need 3 wood and 2 fiber for the basic tool.";
            }
            return;
        }

        if (recipeName == "scanner")
        {
            if (TrySpend(new Dictionary<string, int> { ["wood"] = 2, ["crystal"] = 2, ["copper"] = 1 }))
            {
                inventory["scanner"] += 1;
                statusText = "Aurora scanner crafted.";
                objectiveText = "Use the scanner by reaching the signal tower.";
                questStage = Math.Max(questStage, 2);
            }
            else
            {
                statusText = "Need 2 wood, 2 crystal and 1 copper for the Aurora scanner.";
            }
            return;
        }

        if (recipeName == "shelter")
        {
            if (TrySpend(new Dictionary<string, int> { ["wood"] = 4, ["stone"] = 2, ["fiber"] = 2 }))
            {
                inventory["shelter"] += 1;
                statusText = "Shelter pieces crafted.";
            }
            else
            {
                statusText = "Need 4 wood, 2 stone and 2 fiber for the shelter kit.";
            }
        }
    }

    private bool TrySpend(Dictionary<string, int> cost)
    {
        foreach (var entry in cost)
        {
            if (inventory.GetValueOrDefault(entry.Key, 0) < entry.Value)
            {
                return false;
            }
        }

        foreach (var entry in cost)
        {
            inventory[entry.Key] -= entry.Value;
        }

        return true;
    }

    private void UpdateObjective()
    {
        if (questStage == 0)
        {
            objectiveText = $"Repair the capsule: {inventory["wood"]}/3 wood, {inventory["fiber"]}/2 fiber, {inventory["stone"]}/2 stone";
        }
        else if (questStage == 1)
        {
            objectiveText = "Find Mira and uncover the truth behind Aurora.";
        }
        else if (questStage == 2)
        {
            objectiveText = "Craft the scanner and investigate the signal tower.";
        }
        else if (questStage == 3)
        {
            objectiveText = "Return to Mira and learn what the shard remembers.";
        }
        else if (questStage == 4)
        {
            objectiveText = "Return to the first camp and place the Aurora Shard.";
        }
        else if (gameWon)
        {
            objectiveText = "Aurora remembers. The story is awakening.";
        }
    }

    private void Draw()
    {
        BeginDrawing();
        ClearBackground(GetSkyColor());

        BeginMode3D(camera);
        DrawTerrain();
        DrawTrees();
        DrawResources();
        DrawStructures();
        DrawNpcs();
        DrawEnemies();
        DrawPlayer();
        DrawSignalTower();
        EndMode3D();

        DrawWorldLabels();
        DrawHud();
        if (showMap) DrawMap();
        if (showJournal) DrawJournal();
        EndDrawing();
    }

    private Color GetSkyColor()
    {
        var t = (float)Math.Sin((dayTime - 0.25f) * Math.PI * 2f) * 0.5f + 0.5f;
        var r = (byte)(20 + t * 70);
        var g = (byte)(40 + t * 110);
        var b = (byte)(80 + t * 120);
        return new Color(r, g, b, (byte)255);
    }

    private void DrawTerrain()
    {
        DrawPlane(new Vector3(0f, 0f, 0f), new Vector2(200f, 200f), new Color((byte)56, (byte)122, (byte)80, (byte)255));
        DrawCube(new Vector3(0f, 0.3f, 0f), 200f, 1f, 200f, new Color((byte)72, (byte)145, (byte)92, (byte)255));
        DrawGrid(40, 4f);

        var shoreColor = new Color((byte)72, (byte)175, (byte)206, (byte)255);
        DrawCube(new Vector3(30f, -0.15f, 0f), 28f, 0.7f, 70f, shoreColor);
        DrawCube(new Vector3(-30f, -0.15f, 0f), 22f, 0.7f, 74f, shoreColor);
    }

    private void DrawTrees()
    {
        foreach (var tree in trees)
        {
            var sway = MathF.Sin(animationTime * 1.4f + tree.Position.X) * 0.08f;
            DrawCylinder(tree.Position, 0.35f, 0.75f, 2.5f, 12, new Color((byte)115, (byte)82, (byte)50, (byte)255));
            DrawSphere(tree.Position + new Vector3(sway, 2.4f, sway * 0.5f), tree.Height, new Color((byte)56, (byte)136, (byte)92, (byte)255));
        }
    }

    private void DrawResources()
    {
        foreach (var resource in resources)
        {
            DrawSphere(resource.Position, 0.65f, resource.Color);
            DrawSphere(resource.Position + new Vector3(0f, 0.7f, 0f), 0.18f, Color.White);
        }
    }

    private void DrawStructures()
    {
        foreach (var structure in structures)
        {
            DrawCube(structure.Position, structure.Size.X, structure.Size.Y, structure.Size.Z, structure.Color);
            if (structure.Kind == "camp" || structure.Kind == "aurora campfire")
            {
                DrawCylinder(structure.Position + new Vector3(0f, 0.55f, 0f), 2.4f, 2.4f, 0.08f, 32, new Color((byte)255, (byte)215, (byte)105, (byte)120));
            }
        }
    }

    private void DrawNpcs()
    {
        foreach (var npc in npcs)
        {
            if (modelAssetsLoaded)
            {
                DrawModel(npcModel, npc.Position - new Vector3(0f, 1.3f, 0f), 1f, npc.Color);
            }
            else
            {
                DrawSphere(npc.Position, 0.85f, npc.Color);
            }
            DrawText(npc.Name, (int)(npc.Position.X * 8f), (int)(npc.Position.Z * 8f), 18, Color.White);
        }
    }

    private void DrawEnemies()
    {
        var dangerActive = dayTime > 0.55f || questStage >= 2;
        if (!dangerActive) return;

        foreach (var enemy in enemies)
        {
            var hover = MathF.Sin(animationTime * 3f + enemy.Position.X) * 0.12f;
            var animatedPosition = enemy.Position + new Vector3(0f, hover, 0f);
            if (modelAssetsLoaded)
            {
                DrawModel(enemyModel, animatedPosition - new Vector3(0f, 1.2f, 0f), 1f, new Color((byte)95, (byte)44, (byte)130, (byte)255));
            }
            else
            {
                DrawSphere(animatedPosition, 0.9f, new Color((byte)95, (byte)44, (byte)130, (byte)255));
                DrawSphere(animatedPosition + new Vector3(0f, 0.65f, 0f), 0.42f, new Color((byte)190, (byte)110, (byte)255, (byte)255));
            }
            DrawLine3D(enemy.Position + new Vector3(-0.35f, 0.9f, 0f), enemy.Position + new Vector3(0.35f, 0.9f, 0f), Color.White);
            DrawCube(enemy.Position + new Vector3(0f, 1.7f, 0f), 1.4f, 0.08f, 0.08f, Color.DarkGray);
            var healthRatio = Math.Clamp(enemy.Health / enemy.MaxHealth, 0f, 1f);
            DrawCube(enemy.Position + new Vector3(-0.7f + healthRatio * 0.7f, 1.7f, 0f), healthRatio * 1.4f, 0.08f, 0.08f, new Color((byte)245, (byte)105, (byte)145, (byte)255));
        }
    }

    private void DrawPlayer()
    {
        var bob = MathF.Sin(animationTime * 5f) * 0.035f;
        var animatedPosition = player.Position + new Vector3(0f, bob, 0f);
        if (modelAssetsLoaded)
        {
            DrawModel(playerModel, new Vector3(animatedPosition.X, 0.25f, animatedPosition.Z), 1f, new Color((byte)160, (byte)210, (byte)255, (byte)255));
        }
        else
        {
            DrawCylinder(animatedPosition, 0.6f, 0.8f, 1.4f, 10, new Color((byte)244, (byte)244, (byte)244, (byte)255));
            DrawSphere(animatedPosition + new Vector3(0f, 1.1f, 0f), 0.55f, new Color((byte)160, (byte)210, (byte)255, (byte)255));
        }
    }

    private void DrawSignalTower()
    {
        var towerPos = new Vector3(34f, 1.0f, 20f);
        DrawCylinder(towerPos, 1.2f, 1.2f, 10f, 10, new Color((byte)96, (byte)119, (byte)165, (byte)255));
        DrawSphere(towerPos + new Vector3(0f, 10.5f, 0f), 2f, new Color((byte)112, (byte)235, (byte)255, (byte)255));
        DrawCylinder(towerPos + new Vector3(0f, 0.1f, 0f), 4.5f, 4.5f, 0.06f, 32, new Color((byte)104, (byte)226, (byte)255, (byte)150));
    }

    private void DrawWorldLabels()
    {
        DrawWorldLabel(new Vector3(-18f, 3.2f, 4f), "CAMP", new Color((byte)255, (byte)220, (byte)120, (byte)255));
        DrawWorldLabel(new Vector3(-2f, 3.4f, 17f), "CRASH CAPSULE", new Color((byte)180, (byte)240, (byte)255, (byte)255));
        DrawWorldLabel(new Vector3(34f, 13.5f, 20f), "AURORA SIGNAL", new Color((byte)125, (byte)240, (byte)255, (byte)255));

        foreach (var npc in npcs)
        {
            DrawWorldLabel(npc.Position + new Vector3(0f, 2.5f, 0f), npc.Name, npc.Color);
        }

        var closestResource = resources
            .OrderBy(resource => Vector3.Distance(resource.Position, player.Position))
            .FirstOrDefault(resource => Vector3.Distance(resource.Position, player.Position) < 16f);
        if (closestResource is not null)
        {
            DrawWorldLabel(closestResource.Position + new Vector3(0f, 1.4f, 0f), closestResource.Type.ToUpperInvariant(), closestResource.Color);
        }
    }

    private void DrawWorldLabel(Vector3 worldPosition, string text, Color color)
    {
        var screenPosition = GetWorldToScreen(worldPosition, camera);
        if (screenPosition.X < -200f || screenPosition.X > ScreenWidth + 200f || screenPosition.Y < -50f || screenPosition.Y > ScreenHeight + 50f)
        {
            return;
        }

        var width = MeasureText(text, 16);
        DrawRectangle((int)screenPosition.X - width / 2 - 6, (int)screenPosition.Y - 3, width + 12, 22, new Color((byte)8, (byte)17, (byte)24, (byte)205));
        DrawText(text, (int)screenPosition.X - width / 2, (int)screenPosition.Y, 16, color);
    }

    private void DrawHud()
    {
        var timeText = dayTime > 0.5f ? "Night" : "Day";
        DrawRectangle(20, 20, 260, 68, new Color((byte)12, (byte)15, (byte)25, (byte)180));
        DrawText("Echoes of Aurora", 30, 30, 24, Color.White);
        DrawText(timeText, 30, 58, 20, new Color((byte)160, (byte)220, (byte)255, (byte)255));

        DrawRectangle(20, 620, 240, 70, new Color((byte)15, (byte)25, (byte)32, (byte)200));
        DrawText("Health", 30, 634, 18, Color.White);
        DrawRectangle(120, 636, 120, 14, Color.DarkGray);
        DrawRectangle(120, 636, (int)(120f * (player.Health / 100f)), 14, new Color((byte)255, (byte)115, (byte)115, (byte)255));

        DrawText("Stamina", 30, 660, 18, Color.White);
        DrawRectangle(120, 666, 120, 14, Color.DarkGray);
        DrawRectangle(120, 666, (int)(120f * (player.Stamina / 100f)), 14, new Color((byte)125, (byte)218, (byte)255, (byte)255));
        DrawText("Hunger", 280, 634, 18, Color.White);
        DrawRectangle(370, 636, 120, 14, Color.DarkGray);
        DrawRectangle(370, 636, (int)(120f * (player.Hunger / 100f)), 14, new Color((byte)242, (byte)194, (byte)96, (byte)255));
        DrawText("Thirst", 280, 660, 18, Color.White);
        DrawRectangle(370, 666, 120, 14, Color.DarkGray);
        DrawRectangle(370, 666, (int)(120f * (player.Thirst / 100f)), 14, new Color((byte)92, (byte)190, (byte)240, (byte)255));

        DrawRectangle(ScreenWidth - 400, 20, 360, 180, new Color((byte)10, (byte)18, (byte)28, (byte)190));
        DrawText("Inventory", ScreenWidth - 380, 35, 22, new Color((byte)132, (byte)230, (byte)255, (byte)255));
        var inventoryText = string.Join("\n", inventory
            .Where(kvp => kvp.Value > 0)
            .Select(kvp => $"{kvp.Key}: {kvp.Value}"));
        if (string.IsNullOrWhiteSpace(inventoryText)) inventoryText = "Empty";
        DrawText(inventoryText, ScreenWidth - 380, 70, 18, Color.White);

        DrawRectangle(340, 20, 600, 80, new Color((byte)8, (byte)16, (byte)22, (byte)190));
        DrawText("Objective", 360, 35, 18, new Color((byte)160, (byte)230, (byte)255, (byte)255));
        DrawText(objectiveText, 360, 58, 18, Color.White);

        DrawRectangle(320, ScreenHeight - 120, 640, 70, new Color((byte)12, (byte)22, (byte)26, (byte)200));
        DrawText(statusText, 340, ScreenHeight - 100, 20, Color.White);

        DrawText("M Map   J Journal   F5 Save   F9 Load", ScreenWidth - 390, ScreenHeight - 30, 16, new Color((byte)205, (byte)220, (byte)225, (byte)255));
        DrawText("Left click / Space: attack   4: eat berries   5: drink water", 30, 110, 16, new Color((byte)220, (byte)220, (byte)230, (byte)255));

        var nearbyResource = resources.FirstOrDefault(resource => Vector3.Distance(resource.Position, player.Position) < 2.8f);
        var nearbyNpc = npcs.FirstOrDefault(npc => Vector3.Distance(npc.Position, player.Position) < 3.2f);
        if (nearbyResource is not null)
        {
            DrawText($"E  Collect {nearbyResource.Type}", ScreenWidth / 2 - 100, 112, 20, Color.White);
        }
        else if (nearbyNpc is not null)
        {
            DrawText($"E  Talk to {nearbyNpc.Name}", ScreenWidth / 2 - 100, 112, 20, Color.White);
        }

        if (gameWon)
        {
            var winRect = new Rectangle(300f, 250f, 680f, 180f);
            DrawRectangle((int)winRect.X, (int)winRect.Y, (int)winRect.Width, (int)winRect.Height, new Color((byte)5, (byte)14, (byte)22, (byte)220));
            DrawText("Aurora has awakened.", 420, 295, 30, Color.White);
            DrawText("The memories of the planet are finally speaking.", 360, 340, 20, new Color((byte)140, (byte)220, (byte)255, (byte)255));
        }
    }

    private void DrawMap()
    {
        DrawRectangle(250, 120, 780, 480, new Color((byte)8, (byte)18, (byte)24, (byte)235));
        DrawText("FIELD MAP", 285, 150, 28, new Color((byte)145, (byte)235, (byte)255, (byte)255));
        DrawRectangle(320, 205, 640, 320, new Color((byte)37, (byte)86, (byte)70, (byte)255));
        DrawCircle(640, 365, 8f, Color.White);
        DrawText("YOU", 653, 355, 16, Color.White);
        DrawCircle(505, 390, 7f, new Color((byte)125, (byte)220, (byte)255, (byte)255));
        DrawText("Mira", 518, 380, 16, Color.White);
        DrawCircle(910, 460, 9f, new Color((byte)112, (byte)235, (byte)255, (byte)255));
        DrawText("SIGNAL", 820, 480, 16, Color.White);
        DrawText("M to close", 285, 555, 18, new Color((byte)190, (byte)205, (byte)210, (byte)255));
    }

    private void DrawJournal()
    {
        DrawRectangle(250, 120, 780, 480, new Color((byte)8, (byte)18, (byte)24, (byte)235));
        DrawText("AURORA JOURNAL", 285, 150, 28, new Color((byte)145, (byte)235, (byte)255, (byte)255));
        DrawText("CURRENT OBJECTIVE", 285, 215, 18, new Color((byte)160, (byte)220, (byte)235, (byte)255));
        DrawText(objectiveText, 285, 250, 21, Color.White);
        DrawText("FIELD NOTES", 285, 325, 18, new Color((byte)160, (byte)220, (byte)235, (byte)255));
        DrawText("The forest stores memories in light, water and stone.", 285, 360, 19, Color.White);
        DrawText("Mira believes the signal tower can wake the old colony.", 285, 392, 19, Color.White);
        DrawText("Build a shelter when the night becomes dangerous.", 285, 424, 19, Color.White);
        DrawText("J to close", 285, 555, 18, new Color((byte)190, (byte)205, (byte)210, (byte)255));
    }
}

internal sealed class SaveData
{
    public Vector3 PlayerPosition { get; set; }
    public float Health { get; set; }
    public float Stamina { get; set; }
    public float Hunger { get; set; }
    public float Thirst { get; set; }
    public int QuestStage { get; set; }
    public bool GameWon { get; set; }
    public Dictionary<string, int> Inventory { get; set; } = new();
    public HashSet<string> CollectedResources { get; set; } = new();
}

internal sealed class Player
{
    public Vector3 Position { get; set; }
    public float Speed { get; set; }
    public float Radius { get; set; }
    public float Health { get; set; }
    public float Stamina { get; set; }
    public float Hunger { get; set; }
    public float Thirst { get; set; }
}

internal sealed class ResourceNode
{
    public ResourceNode(string type, Vector3 position, Color color)
    {
        Type = type;
        Position = position;
        Color = color;
    }

    public string Type { get; }
    public Vector3 Position { get; }
    public Color Color { get; }
}

internal sealed class Structure
{
    public Structure(Vector3 position, Vector3 size, Color color, string kind)
    {
        Position = position;
        Size = size;
        Color = color;
        Kind = kind;
    }

    public Vector3 Position { get; }
    public Vector3 Size { get; }
    public Color Color { get; }
    public string Kind { get; }
}

internal sealed class Tree
{
    public Tree(Vector3 position, float height)
    {
        Position = position;
        Height = height;
    }

    public Vector3 Position { get; }
    public float Height { get; }
}

internal sealed class Npc
{
    public Npc(string name, Vector3 position, Color color)
    {
        Name = name;
        Position = position;
        Color = color;
    }

    public string Name { get; }
    public Vector3 Position { get; }
    public Color Color { get; }
}

internal sealed class Enemy
{
    public Enemy(string name, Vector3 position, float health)
    {
        Name = name;
        Position = position;
        Anchor = position;
        Health = health;
        MaxHealth = health;
        Speed = 3.2f;
        Damage = 9f;
    }

    public string Name { get; }
    public Vector3 Position { get; set; }
    public Vector3 Anchor { get; }
    public float Health { get; set; }
    public float MaxHealth { get; }
    public float Speed { get; }
    public float Damage { get; }
    public float DamageCooldown { get; set; }
    public float PatrolTime { get; set; }
}
