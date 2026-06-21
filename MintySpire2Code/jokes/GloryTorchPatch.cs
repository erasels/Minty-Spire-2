using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace MintySpire2.MintySpire2Code.jokes;

/**
 * Credits to cany0udance, this allows Torches to be clicked to toggle their state.
 */

[HarmonyPatch(typeof(NCombatBackground), "Create")]
public static class GloryTorchPatch
{
    private const bool ShowDebugHitboxes = false;

    private static readonly List<TorchFadeState> _activeFades = new();
    private class TorchFadeState
    {
        public Node Torch;
        public bool FadingOut;
        public float Timer;
        public float Duration;
    }

    private static readonly List<ShaderFireFadeState> _activeShaderFades = new();
    private class ShaderFireFadeState
    {
        public Sprite2D Sprite;
        public Vector2 OriginalScale;
        public CpuParticles2D NearestLight;
        public bool FadingOut;
        public float Timer;
        public float Duration;
    }

    static void Postfix(NCombatBackground __result)
    {
        __result.Ready += () =>
        {
            SetupTorches(__result, "Layer_00", "torch");
            SetupTorches(__result, "Layer_01", "torch");
            SetupTorches(__result, "Layer_02", "torch");
            SetupTorches(__result, "Layer_03", "torch");
            SetupTorches(__result, "Foreground", "torch");
            SetupShaderFires(__result);
        };
    }

    private static Vector2 GetFlameCenter(Node torch)
    {
        float avgX = 0f, avgY = 0f;
        int count = 0;
        foreach (var child in torch.GetChildren())
        {
            if (child is CpuParticles2D p)
            {
                avgX += p.Position.X;
                avgY += p.Position.Y;
                count++;
            }
        }
        if (count > 0)
            return new Vector2(avgX / count, avgY / count);
        return Vector2.Zero;
    }

    private static Vector2 GetNodePosition(Node node)
    {
        if (node is Node2D n2d) return n2d.Position;
        if (node is Control ctrl) return ctrl.Position;
        return Vector2.Zero;
    }

    private static Vector2 GetNodeScale(Node node)
    {
        if (node is Node2D n2d) return n2d.Scale;
        if (node is Control ctrl) return ctrl.Scale;
        return Vector2.One;
    }

    private static void SetupTorches(NCombatBackground background, string layerName, string torchPrefix)
    {
        var layerNode = background.GetNodeOrNull(layerName);
        if (layerNode == null) return;

        for (int i = 0; i < layerNode.GetChildCount(); i++)
        {
            var sceneRoot = layerNode.GetChild(i);
            var torchNodes = new List<Node>();
            foreach (var child in sceneRoot.GetChildren())
            {
                if (!child.Name.ToString().StartsWith(torchPrefix)) continue;
                if (child is not Node2D && child is not Control) continue;
                torchNodes.Add(child);
            }
            if (torchNodes.Count == 0) continue;

            foreach (var torch in torchNodes)
            {
                foreach (var child in torch.GetChildren())
                {
                    if (child is CpuParticles2D particles)
                        particles.SetMeta("original_amount", particles.Amount);
                }
            }

            var torchRects = new List<(Node torch, Rect2 localRect)>();
            foreach (var torch in torchNodes)
            {
                var torchPos = GetNodePosition(torch);
                var flameOffset = GetFlameCenter(torch);
                var flameCenterPos = torchPos + flameOffset;
                var torchScale = GetNodeScale(torch);
                float scaleFactor = Math.Max(torchScale.X, torchScale.Y);
                if (scaleFactor <= 0f) scaleFactor = 1f;
                var baseSize = layerName == "Layer_03" ? new Vector2(50f, 65f) : new Vector2(40f, 50f);
                var clickSize = baseSize * scaleFactor;
                var localPos = new Vector2(
                    flameCenterPos.X - clickSize.X / 2f,
                    flameCenterPos.Y - clickSize.Y / 2f
                );
                torchRects.Add((torch, new Rect2(localPos, clickSize)));

                if (ShowDebugHitboxes)
                {
                    var debugRect = new ColorRect();
                    debugRect.Color = new Color(1f, 0f, 0f, 0.4f);
                    debugRect.Size = clickSize;
                    debugRect.Position = localPos;
                    debugRect.MouseFilter = Control.MouseFilterEnum.Ignore;
                    sceneRoot.AddChild(debugRect);
                }
            }

            var capturedSceneRoot = sceneRoot;
            bool wasPressed = false;
            background.GetTree().ProcessFrame += () =>
            {
                if (!GodotObject.IsInstanceValid(background)) return;
                if (!GodotObject.IsInstanceValid(capturedSceneRoot)) return;
                float delta = (float)background.GetProcessDeltaTime();

                for (int f = _activeFades.Count - 1; f >= 0; f--)
                {
                    var fade = _activeFades[f];
                    if (!GodotObject.IsInstanceValid(fade.Torch))
                    {
                        _activeFades.RemoveAt(f);
                        continue;
                    }
                    fade.Timer += delta;
                    float t = Math.Min(fade.Timer / fade.Duration, 1f);
                    if (fade.FadingOut)
                    {
                        foreach (var child in fade.Torch.GetChildren())
                        {
                            if (child is CpuParticles2D particles)
                            {
                                var mod = particles.Modulate;
                                mod.A = 1f - t;
                                particles.Modulate = mod;
                            }
                        }
                        if (t >= 1f)
                        {
                            foreach (var child in fade.Torch.GetChildren())
                            {
                                if (child is CpuParticles2D particles)
                                {
                                    particles.Emitting = false;
                                    particles.Visible = false;
                                    var mod = particles.Modulate;
                                    mod.A = 1f;
                                    particles.Modulate = mod;
                                }
                            }
                            _activeFades.RemoveAt(f);
                        }
                    }
                    else
                    {
                        foreach (var child in fade.Torch.GetChildren())
                        {
                            if (child is not CpuParticles2D particles) continue;
                            var mod = particles.Modulate;
                            mod.A = t;
                            particles.Modulate = mod;
                            if (t >= 1f)
                            {
                                mod.A = 1f;
                                particles.Modulate = mod;
                            }
                        }
                        if (t >= 1f)
                            _activeFades.RemoveAt(f);
                    }
                }

                bool isPressed = Input.IsMouseButtonPressed(MouseButton.Left);
                bool justPressed = isPressed && !wasPressed;
                wasPressed = isPressed;
                if (!justPressed) return;
                if (!IsCombatInFocus(background)) return;

                var mousePos = background.GetViewport().GetMousePosition();
                var localMouse = capturedSceneRoot is CanvasItem ci
                    ? ci.GetGlobalTransform().AffineInverse() * mousePos
                    : mousePos;

                foreach (var (torch, localRect) in torchRects)
                {
                    if (!GodotObject.IsInstanceValid(torch)) continue;
                    if (localRect.HasPoint(localMouse))
                    {
                        _activeFades.RemoveAll(fd => fd.Torch == torch);
                        bool isCurrentlyLit = false;
                        foreach (var child in torch.GetChildren())
                        {
                            if (child is CpuParticles2D p)
                            {
                                isCurrentlyLit = p.Emitting;
                                break;
                            }
                        }
                        bool fadingOut = isCurrentlyLit;

                        if (!fadingOut)
                        {
                            foreach (var child in torch.GetChildren())
                            {
                                if (child is CpuParticles2D particles)
                                {
                                    if (particles.HasMeta("original_amount"))
                                        particles.Amount = particles.GetMeta("original_amount").AsInt32();
                                    var mod = particles.Modulate;
                                    mod.A = 0f;
                                    particles.Modulate = mod;
                                    particles.Visible = true;
                                    particles.Emitting = true;
                                }
                            }
                        }

                        _activeFades.Add(new TorchFadeState
                        {
                            Torch = torch,
                            FadingOut = fadingOut,
                            Timer = 0f,
                            Duration = fadingOut ? 0.75f : 0.4f
                        });
                        return;
                    }
                }
            };
        }
    }

    private static void SetupShaderFires(NCombatBackground background)
    {
        var firesNode = background.GetNodeOrNull("fires");
        if (firesNode == null) return;

        var fireSprites = new List<Sprite2D>();
        var fireLights = new List<CpuParticles2D>();

        foreach (var child in firesNode.GetChildren())
        {
            if (child is Sprite2D sprite)
                fireSprites.Add(sprite);
            else if (child is CpuParticles2D light)
                fireLights.Add(light);
        }

        foreach (var sprite in fireSprites)
        {
            if (sprite.Material is ShaderMaterial original)
            {
                var unique = (ShaderMaterial)original.Duplicate();
                if (unique.Shader is Shader origShader)
                {
                    var newShader = (Shader)origShader.Duplicate();
                    var code = newShader.Code;
                    code = code.Replace(
                        "uniform vec2 OuterStep",
                        "uniform float fade_alpha = 1.0;\nuniform float fade_mode = 0.0;\nuniform vec2 OuterStep"
                    );
                    code = code.Replace(
                        "COLOR.a = n_out27p0;",
                        @"{
        float threshold = 1.0 - fade_alpha;
        float mask;
        if (fade_mode < 0.5) {
            float dissolve_noise;
            {
                vec2 p = UV * 6.0 + TIME * vec2(0.3, -0.8);
                float n = sin(p.x * 1.7 + p.y * 2.3 + TIME * 0.4) * 0.5 + 0.5;
                n += (sin(p.x * 4.1 - p.y * 3.7 + TIME * 0.7) * 0.5 + 0.5) * 0.5;
                n += (sin(p.x * 8.3 + p.y * 7.1 + TIME * 1.1) * 0.5 + 0.5) * 0.25;
                dissolve_noise = n / 1.75;
            }
            mask = smoothstep(threshold - 0.15, threshold + 0.15, dissolve_noise);
        } else {
            float wipe_noise = sin(UV.x * 12.0 + TIME * 2.0) * 0.05;
            mask = smoothstep(threshold - 0.1, threshold + 0.1, UV.y + wipe_noise);
        }
        COLOR.a = n_out27p0 * mask;
    }"
                    );
                    newShader.Code = code;
                    unique.Shader = newShader;
                }
                sprite.Material = unique;
                unique.SetShaderParameter("fade_alpha", 1.0f);
                unique.SetShaderParameter("fade_mode", 0.0f);
            }
        }

        var fireRects = new List<(Sprite2D sprite, CpuParticles2D nearestLight, Rect2 rect)>();

// Pre-compute which sprite is closest to each light (the "middle" flame)
        var lightOwner = new Dictionary<CpuParticles2D, Sprite2D>();
        foreach (var light in fireLights)
        {
            Sprite2D closest = null;
            float closestDist = float.MaxValue;
            foreach (var sprite in fireSprites)
            {
                float dist = sprite.Position.DistanceTo(light.Position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = sprite;
                }
            }
            if (closest != null)
                lightOwner[light] = closest;
        }

        foreach (var sprite in fireSprites)
        {
            // Only pair this sprite with a light if it's that light's closest sprite
            CpuParticles2D nearestLight = null;
            float nearestDist = float.MaxValue;
            foreach (var light in fireLights)
            {
                if (lightOwner.TryGetValue(light, out var owner) && owner != sprite)
                    continue;
                float dist = sprite.Position.DistanceTo(light.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestLight = light;
                }
            }

            var clickSize = new Vector2(45f, 55f);
            var localPos = new Vector2(
                sprite.Position.X - clickSize.X / 2f,
                sprite.Position.Y - clickSize.Y / 2f
            );
            fireRects.Add((sprite, nearestLight, new Rect2(localPos, clickSize)));

            if (ShowDebugHitboxes)
            {
                var debugRect = new ColorRect();
                debugRect.Color = new Color(0f, 1f, 0f, 0.4f);
                debugRect.Size = clickSize;
                debugRect.Position = localPos;
                debugRect.MouseFilter = Control.MouseFilterEnum.Ignore;
                firesNode.AddChild(debugRect);
            }
        }

        const float fadeOutDuration = 1.2f;
        const float fadeInDuration = 1.0f;

        bool wasPressed = false;

        background.GetTree().ProcessFrame += () =>
        {
            if (!GodotObject.IsInstanceValid(background)) return;
            float delta = (float)background.GetProcessDeltaTime();

            for (int f = _activeShaderFades.Count - 1; f >= 0; f--)
            {
                var fade = _activeShaderFades[f];
                if (!GodotObject.IsInstanceValid(fade.Sprite))
                {
                    _activeShaderFades.RemoveAt(f);
                    continue;
                }

                fade.Timer += delta;
                float t = Math.Min(fade.Timer / fade.Duration, 1f);

                if (fade.FadingOut)
                {
                    float easedAlpha = (1f - t) * (1f - t);
                    if (fade.Sprite.Material is ShaderMaterial sm)
                        sm.SetShaderParameter("fade_alpha", easedAlpha);
                }
                else
                {
                    float easedAlpha = 1f - (1f - t) * (1f - t);
                    if (fade.Sprite.Material is ShaderMaterial sm)
                        sm.SetShaderParameter("fade_alpha", easedAlpha);
                }

                if (fade.NearestLight != null && GodotObject.IsInstanceValid(fade.NearestLight))
                {
                    float maxAlpha = 0f;
                    foreach (var (sprite, nearestLight, _) in fireRects)
                    {
                        if (nearestLight != fade.NearestLight) continue;
                        if (!GodotObject.IsInstanceValid(sprite)) continue;
                        if (!sprite.Visible) continue;

                        float spriteAlpha = 1f;
                        foreach (var otherFade in _activeShaderFades)
                        {
                            if (otherFade.Sprite == sprite)
                            {
                                float otherT = Math.Min(otherFade.Timer / otherFade.Duration, 1f);
                                spriteAlpha = otherFade.FadingOut
                                    ? (1f - otherT) * (1f - otherT)
                                    : 1f - (1f - otherT) * (1f - otherT);
                                break;
                            }
                        }
                        maxAlpha = Math.Max(maxAlpha, spriteAlpha);
                    }

                    float currentLightAlpha = fade.NearestLight.Modulate.A;
                    float lerpSpeed = fade.FadingOut ? 1.5f : 4f;
                    float newAlpha = currentLightAlpha + (maxAlpha - currentLightAlpha) * Math.Min(delta * lerpSpeed, 1f);

                    var mod = fade.NearestLight.Modulate;
                    mod.A = newAlpha;
                    fade.NearestLight.Modulate = mod;

                    if (newAlpha <= 0.01f && maxAlpha <= 0f)
                    {
                        fade.NearestLight.Emitting = false;
                        fade.NearestLight.Visible = false;
                    }
                }

                if (t >= 1f)
                {
                    if (fade.FadingOut)
                        fade.Sprite.Visible = false;

                    if (fade.Sprite.Material is ShaderMaterial sm2)
                    {
                        sm2.SetShaderParameter("fade_alpha", 1.0f);
                        sm2.SetShaderParameter("fade_mode", 0.0f);
                    }

                    if (!fade.FadingOut && fade.NearestLight != null && GodotObject.IsInstanceValid(fade.NearestLight))
                    {
                        var mod = fade.NearestLight.Modulate;
                        mod.A = 1f;
                        fade.NearestLight.Modulate = mod;
                    }

                    _activeShaderFades.RemoveAt(f);
                }
            }

            bool isPressed = Input.IsMouseButtonPressed(MouseButton.Left);
            bool justPressed = isPressed && !wasPressed;
            wasPressed = isPressed;
            if (!justPressed) return;
            if (!IsCombatInFocus(background)) return;

            var mousePos = background.GetViewport().GetMousePosition();
            var localMouse = firesNode is CanvasItem ci
                ? ci.GetGlobalTransform().AffineInverse() * mousePos
                : mousePos;

            foreach (var (sprite, nearestLight, rect) in fireRects)
            {
                if (!GodotObject.IsInstanceValid(sprite)) continue;
                if (rect.HasPoint(localMouse))
                {
                    float currentAlpha = 1f;
                    bool wasFading = false;
                    foreach (var existingFade in _activeShaderFades)
                    {
                        if (existingFade.Sprite == sprite)
                        {
                            float p = Math.Min(existingFade.Timer / existingFade.Duration, 1f);
                            currentAlpha = existingFade.FadingOut
                                ? (1f - p) * (1f - p)
                                : 1f - (1f - p) * (1f - p);
                            wasFading = true;
                            break;
                        }
                    }

                    _activeShaderFades.RemoveAll(fd => fd.Sprite == sprite);

                    bool isLit = sprite.Visible && currentAlpha > 0f;
                    bool fadingOut = isLit;

                    if (!fadingOut)
                    {
                        sprite.Visible = true;
                        float startAlpha = wasFading ? currentAlpha : 0f;

                        if (sprite.Material is ShaderMaterial sm)
                        {
                            sm.SetShaderParameter("fade_alpha", startAlpha);
                            sm.SetShaderParameter("fade_mode", 1.0f);
                        }

                        if (nearestLight != null && GodotObject.IsInstanceValid(nearestLight))
                        {
                            nearestLight.Visible = true;
                            nearestLight.Emitting = true;
                        }

                        float resumeT = startAlpha > 0f
                            ? 1f - (float)Math.Sqrt(1f - startAlpha)
                            : 0f;

                        _activeShaderFades.Add(new ShaderFireFadeState
                        {
                            Sprite = sprite,
                            OriginalScale = sprite.Scale,
                            NearestLight = nearestLight,
                            FadingOut = false,
                            Timer = resumeT * fadeInDuration,
                            Duration = fadeInDuration
                        });
                    }
                    else
                    {
                        if (sprite.Material is ShaderMaterial sm)
                            sm.SetShaderParameter("fade_mode", 0.0f);

                        float resumeT = currentAlpha < 1f
                            ? 1f - (float)Math.Sqrt(currentAlpha)
                            : 0f;

                        _activeShaderFades.Add(new ShaderFireFadeState
                        {
                            Sprite = sprite,
                            OriginalScale = sprite.Scale,
                            NearestLight = nearestLight,
                            FadingOut = true,
                            Timer = resumeT * fadeOutDuration,
                            Duration = fadeOutDuration
                        });
                    }
                    return;
                }
            }
        };
    }
    
    private static bool IsCombatInFocus(Node context)
    {
        var hoveredControl = context.GetViewport().GuiGetHoveredControl();
        if (hoveredControl == null)
            return true;

        Node current = hoveredControl;
        while (current != null)
        {
            if (current is NCombatRoom)
                return true;
            current = current.GetParent();
        }
        return false;
    }
}