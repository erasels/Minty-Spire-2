using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace MintySpire2.MintySpire2Code.jokes;

[HarmonyPatch(typeof(NAncientEventLayout), "InitializeVisuals")]
public static class TezcataraFirePatch
{
    private const bool ShowDebugHitboxes = false;
    private static readonly List<FireFadeState> _activeFades = new();
 
    private class FireFadeState
    {
        public Node FireNode;
        public bool FadingOut;
        public float Timer;
        public float Duration;
    }
 
    private class FireInfo
    {
        public Node Node;
        public Vector2 LocalFlameCenter;
    }
 
    // ───────────────────────── HARMONY POSTFIX ─────────────────────────
 
    static void Postfix(NAncientEventLayout __instance)
    {
        var bgContainer = AccessTools.Field(typeof(NAncientEventLayout), "_ancientBgContainer")
            ?.GetValue(__instance) as Node;
        if (bgContainer == null || bgContainer.GetChildCount() == 0) return;
 
        var sceneRoot = bgContainer.GetChild(bgContainer.GetChildCount() - 1);
        if (sceneRoot == null) return;
 
        // Only set up fires if this scene has a FireContainer or SpineSprite with flames
        if (sceneRoot.GetNodeOrNull("FireContainer") == null &&
            sceneRoot.GetNodeOrNull("SpineSprite") == null)
            return;
 
        if (sceneRoot.IsNodeReady())
            Setup(sceneRoot);
        else
            sceneRoot.Ready += () => Setup(sceneRoot);
    }
 
    // ───────────────────────── ENTRY POINT ─────────────────────────
 
    private static void Setup(Node sceneRoot)
    {
        var fires = new List<FireInfo>();
 
        // Collect static fires from FireContainer
        var fc = sceneRoot.GetNodeOrNull("FireContainer");
        if (fc != null)
        {
            foreach (var child in fc.GetChildren())
            {
                if (child is Node2D && HasShaderSprites(child))
                    fires.Add(new FireInfo { Node = child, LocalFlameCenter = ComputeFlameCenter(child) });
            }
        }
 
        // Collect animated fires from spine bones
        var spine = sceneRoot.GetNodeOrNull("SpineSprite");
        if (spine != null)
            FindBoneFires(spine, fires);
 
        if (fires.Count == 0) return;
 
        foreach (var f in fires)
        {
            PrepareShaders(f.Node);
            StoreParticleDefaults(f.Node);
        }
 
        if (ShowDebugHitboxes)
            CreateDebugRects(sceneRoot, fires);
 
        bool wasPressed = false;
 
        sceneRoot.GetTree().ProcessFrame += () =>
        {
            if (!GodotObject.IsInstanceValid(sceneRoot)) return;
            float delta = (float)sceneRoot.GetProcessDeltaTime();
 
            UpdateFades(delta);
 
            bool isPressed = Input.IsMouseButtonPressed(MouseButton.Left);
            bool justPressed = isPressed && !wasPressed;
            wasPressed = isPressed;
            if (!justPressed) return;
            if (!IsEventInFocus(sceneRoot)) return;
 
            var mousePos = sceneRoot.GetViewport().GetMousePosition();
 
            foreach (var fire in fires)
            {
                if (!GodotObject.IsInstanceValid(fire.Node)) continue;
                if (fire.Node is not CanvasItem ci) continue;
 
                var globalCenter = ci.GetGlobalTransform() * fire.LocalFlameCenter;
                var clickHalf = new Vector2(18f, 23f);
                var rect = new Rect2(globalCenter - clickHalf, clickHalf * 2f);
 
                if (!rect.HasPoint(mousePos)) continue;
 
                HandleClick(fire);
                return;
            }
        };
    }
 
    // ───────────────────────── DISCOVERY ─────────────────────────
 
    private static bool HasShaderSprites(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Sprite2D s && s.Name.ToString().StartsWith("SteppedFire"))
                return true;
        }
        return false;
    }
 
    private static void FindBoneFires(Node parent, List<FireInfo> fires)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is Node2D && HasShaderSprites(child))
                fires.Add(new FireInfo { Node = child, LocalFlameCenter = ComputeFlameCenter(child) });
            else
                FindBoneFires(child, fires);
        }
    }
 
    private static Vector2 ComputeFlameCenter(Node fire)
    {
        float sumX = 0f, sumY = 0f;
        int count = 0;
        foreach (var child in fire.GetChildren())
        {
            if (child is Sprite2D s && s.Name.ToString().StartsWith("SteppedFire"))
            {
                sumX += s.Position.X;
                sumY += s.Position.Y;
                count++;
            }
        }
        return count > 0 ? new Vector2(sumX / count, sumY / count) : Vector2.Zero;
    }
 
    // ───────────────────────── SHADER PREP ─────────────────────────
 
    // Cache: original Shader RID → patched Shader. Only 2 unique shaders in the scene,
    // so we compile the modified code twice total instead of ~180 times.
    private static readonly Dictionary<Rid, Shader> _patchedShaderCache = new();
 
    private static Shader GetOrCreatePatchedShader(Shader origShader)
    {
        var rid = origShader.GetRid();
        if (_patchedShaderCache.TryGetValue(rid, out var cached))
            return cached;
 
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
        _patchedShaderCache[rid] = newShader;
        return newShader;
    }
 
    private static void PrepareShaders(Node fire)
    {
        foreach (var child in fire.GetChildren())
        {
            if (child is not Sprite2D sprite) continue;
            if (sprite.Material is not ShaderMaterial original) continue;
 
            // Duplicate only the material (cheap), share the patched shader (expensive to compile)
            var unique = (ShaderMaterial)original.Duplicate();
            if (unique.Shader is Shader origShader)
                unique.Shader = GetOrCreatePatchedShader(origShader);
 
            sprite.Material = unique;
            unique.SetShaderParameter("fade_alpha", 1.0f);
            unique.SetShaderParameter("fade_mode", 0.0f);
        }
    }
 
    private static void StoreParticleDefaults(Node fire)
    {
        foreach (var child in fire.GetChildren())
        {
            if (child is CpuParticles2D cpu)
                cpu.SetMeta("original_amount", cpu.Amount);
            else if (child is GpuParticles2D gpu)
                gpu.SetMeta("original_amount", gpu.Amount);
        }
    }
 
    // ───────────────────────── FADE UPDATES ─────────────────────────
 
    private static void UpdateFades(float delta)
    {
        for (int i = _activeFades.Count - 1; i >= 0; i--)
        {
            var fade = _activeFades[i];
            if (!GodotObject.IsInstanceValid(fade.FireNode))
            {
                _activeFades.RemoveAt(i);
                continue;
            }
 
            fade.Timer += delta;
            float t = Math.Min(fade.Timer / fade.Duration, 1f);
            float alpha = fade.FadingOut
                ? (1f - t) * (1f - t)
                : 1f - (1f - t) * (1f - t);
 
            ApplyAlpha(fade.FireNode, alpha);
 
            if (t >= 1f)
            {
                FinishFade(fade);
                _activeFades.RemoveAt(i);
            }
        }
    }
 
    private static void ApplyAlpha(Node fire, float alpha)
    {
        foreach (var child in fire.GetChildren())
        {
            if (child is Sprite2D sprite && sprite.Material is ShaderMaterial sm)
            {
                sm.SetShaderParameter("fade_alpha", alpha);
            }
            else if (child is CpuParticles2D cpu)
            {
                var mod = cpu.Modulate;
                mod.A = alpha;
                cpu.Modulate = mod;
            }
            else if (child is GpuParticles2D gpu)
            {
                var mod = gpu.Modulate;
                mod.A = alpha;
                gpu.Modulate = mod;
            }
        }
    }
 
    private static void FinishFade(FireFadeState fade)
    {
        foreach (var child in fade.FireNode.GetChildren())
        {
            if (fade.FadingOut)
            {
                if (child is Sprite2D sprite)
                {
                    sprite.Visible = false;
                    if (sprite.Material is ShaderMaterial sm)
                    {
                        sm.SetShaderParameter("fade_alpha", 1.0f);
                        sm.SetShaderParameter("fade_mode", 0.0f);
                    }
                }
                else if (child is CpuParticles2D cpu)
                {
                    cpu.Emitting = false;
                    cpu.Visible = false;
                    var mod = cpu.Modulate;
                    mod.A = 1f;
                    cpu.Modulate = mod;
                }
                else if (child is GpuParticles2D gpu)
                {
                    gpu.Emitting = false;
                    gpu.Visible = false;
                    var mod = gpu.Modulate;
                    mod.A = 1f;
                    gpu.Modulate = mod;
                }
            }
            else
            {
                // Fade in complete — restore full alpha
                if (child is Sprite2D sprite && sprite.Material is ShaderMaterial sm)
                {
                    sm.SetShaderParameter("fade_alpha", 1.0f);
                    sm.SetShaderParameter("fade_mode", 0.0f);
                }
                else if (child is CpuParticles2D cpu)
                {
                    var mod = cpu.Modulate;
                    mod.A = 1f;
                    cpu.Modulate = mod;
                }
                else if (child is GpuParticles2D gpu)
                {
                    var mod = gpu.Modulate;
                    mod.A = 1f;
                    gpu.Modulate = mod;
                }
            }
        }
    }
 
    // ───────────────────────── CLICK HANDLING ─────────────────────────
 
    private static void HandleClick(FireInfo fire)
    {
        // Determine current alpha if mid-fade
        float currentAlpha = 1f;
        bool wasFading = false;
        foreach (var existing in _activeFades)
        {
            if (existing.FireNode == fire.Node)
            {
                float p = Math.Min(existing.Timer / existing.Duration, 1f);
                currentAlpha = existing.FadingOut
                    ? (1f - p) * (1f - p)
                    : 1f - (1f - p) * (1f - p);
                wasFading = true;
                break;
            }
        }
 
        _activeFades.RemoveAll(f => f.FireNode == fire.Node);
 
        bool isLit = IsFireLit(fire.Node, currentAlpha);
        bool fadingOut = isLit;
 
        const float fadeOutDuration = 1.0f;
        const float fadeInDuration = 0.8f;
 
        if (!fadingOut)
        {
            // Re-light the fire
            float startAlpha = wasFading ? currentAlpha : 0f;
 
            foreach (var child in fire.Node.GetChildren())
            {
                if (child is Sprite2D sprite)
                {
                    sprite.Visible = true;
                    if (sprite.Material is ShaderMaterial sm)
                    {
                        sm.SetShaderParameter("fade_alpha", startAlpha);
                        sm.SetShaderParameter("fade_mode", 1.0f);
                    }
                }
                else if (child is CpuParticles2D cpu)
                {
                    if (cpu.HasMeta("original_amount"))
                        cpu.Amount = cpu.GetMeta("original_amount").AsInt32();
                    var mod = cpu.Modulate;
                    mod.A = startAlpha;
                    cpu.Modulate = mod;
                    cpu.Visible = true;
                    cpu.Emitting = true;
                }
                else if (child is GpuParticles2D gpu)
                {
                    if (gpu.HasMeta("original_amount"))
                        gpu.Amount = gpu.GetMeta("original_amount").AsInt32();
                    var mod = gpu.Modulate;
                    mod.A = startAlpha;
                    gpu.Modulate = mod;
                    gpu.Visible = true;
                    gpu.Emitting = true;
                }
            }
 
            float resumeT = startAlpha > 0f ? 1f - (float)Math.Sqrt(1f - startAlpha) : 0f;
            _activeFades.Add(new FireFadeState
            {
                FireNode = fire.Node,
                FadingOut = false,
                Timer = resumeT * fadeInDuration,
                Duration = fadeInDuration
            });
        }
        else
        {
            // Extinguish the fire
            foreach (var child in fire.Node.GetChildren())
            {
                if (child is Sprite2D sprite && sprite.Material is ShaderMaterial sm)
                    sm.SetShaderParameter("fade_mode", 0.0f);
            }
 
            float resumeT = currentAlpha < 1f ? 1f - (float)Math.Sqrt(currentAlpha) : 0f;
            _activeFades.Add(new FireFadeState
            {
                FireNode = fire.Node,
                FadingOut = true,
                Timer = resumeT * fadeOutDuration,
                Duration = fadeOutDuration
            });
        }
    }
 
    private static bool IsFireLit(Node fire, float currentAlpha)
    {
        if (currentAlpha > 0f)
        {
            foreach (var child in fire.GetChildren())
            {
                if (child is Sprite2D s && s.Visible) return true;
                if (child is CpuParticles2D cpu && cpu.Emitting) return true;
            }
        }
        return false;
    }
 
    // ───────────────────────── DEBUG ─────────────────────────
 
    private static void CreateDebugRects(Node sceneRoot, List<FireInfo> fires)
    {
        // Only useful for static (FireContainer) fires; bone fire rects would need per-frame updates
        var fc = sceneRoot.GetNodeOrNull("FireContainer");
        if (fc == null) return;
 
        foreach (var fire in fires)
        {
            if (!GodotObject.IsInstanceValid(fire.Node)) continue;
            if (fire.Node is not CanvasItem ci) continue;
 
            // Only add debug rects for FireContainer children
            if (fire.Node.GetParent() != fc) continue;
 
            var globalCenter = ci.GetGlobalTransform() * fire.LocalFlameCenter;
            var clickHalf = new Vector2(18f, 23f);
 
            var debugRect = new ColorRect();
            debugRect.Color = new Color(1f, 0f, 0f, 0.3f);
            debugRect.Size = clickHalf * 2f;
            debugRect.Position = globalCenter - clickHalf;
            debugRect.MouseFilter = Control.MouseFilterEnum.Ignore;
            sceneRoot.AddChild(debugRect);
        }
    }
    
    private static bool IsEventInFocus(Node sceneRoot)
    {
        var hoveredControl = sceneRoot.GetViewport().GuiGetHoveredControl();
        if (hoveredControl == null)
            return true;

        Node current = hoveredControl;
        while (current != null)
        {
            if (current is NEventRoom)
                return true;
            current = current.GetParent();
        }
        return false;
    }
}