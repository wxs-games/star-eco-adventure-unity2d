using UnityEngine;

namespace StarEco.Player
{
    /// <summary>Independent model review scene; does not drive gameplay.</summary>
    public sealed class Player3DAnimationPreview : MonoBehaviour
    {
        public Animator Target;
        public AnimationClip[] Clips;
        private readonly string[] _states = { "Idle", "Run", "Jump", "Fall", "Land" };
        private int _selected;
        private bool _automatic = true;
        private float _elapsed;
        private float _blinkTime;
        private SkinnedMeshRenderer[] _renderers;

        private void Start()
        {
            if (Target == null) Target = GetComponentInChildren<Animator>();
            if (Target == null) { enabled = false; return; }
            Target.applyRootMotion = false;
            Target.enabled = false;
            _renderers = Target.GetComponentsInChildren<SkinnedMeshRenderer>();
            Select(0);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            int clipCount = Clips?.Length ?? 0;
            if (clipCount == 0) return;
            var current = Clips[_selected];
            if (_automatic && _elapsed > Mathf.Max(2f, current != null ? current.length : 0f))
                Select((_selected + 1) % clipCount);
            for (int i = 0; i < Mathf.Min(clipCount, 9); i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { _automatic = false; Select(i); }
            if (Clips != null && _selected < Clips.Length && Clips[_selected] != null)
            {
                var clip = Clips[_selected];
                bool loop = _selected == 0 || _selected == 1 || _selected == 3;
                float time = loop ? _elapsed % clip.length : Mathf.Min(_elapsed, clip.length);
                clip.SampleAnimation(Target.gameObject, time);
            }
            _blinkTime += Time.deltaTime;
            float phase = _blinkTime % 3.2f;
            float weight = phase < .18f ? Mathf.Sin(phase / .18f * Mathf.PI) * 100f : 0f;
            string label = Clips[_selected] != null ? Clips[_selected].name : "";
            bool grip = _selected == 1 || label == "Attack" || label == "Attack02" ||
                        label == "HeavyAttack" || label == "Dash";
            foreach (var renderer in _renderers)
                for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                    if (renderer.sharedMesh.GetBlendShapeName(i).Contains("Blink_"))
                        renderer.SetBlendShapeWeight(i, weight);
                    else if (renderer.sharedMesh.GetBlendShapeName(i).Contains("RunGrip"))
                        renderer.SetBlendShapeWeight(i, grip ? 100f : 0f);
        }

        private void Select(int index)
        {
            _selected = index;
            _elapsed = 0f;
            if (Clips != null && index < Clips.Length && Clips[index] != null)
                Clips[index].SampleAnimation(Target.gameObject, 0f);
        }

        private void OnGUI()
        {
            int count = Clips?.Length ?? 0;
            int columns = Mathf.Clamp((Screen.width - 44) / 106, 1, 5);
            int rows = Mathf.CeilToInt(count / (float)columns);
            GUI.Box(new Rect(12, 12, 20 + columns * 106, 58 + rows * 34), "3D Animation Review / 独立动画预览");
            for (int i = 0; i < count; i++)
            {
                string label = i < _states.Length ? _states[i] : Clips[i] != null ? Clips[i].name : "—";
                string shortcut = i < 9 ? (i + 1) + " " : "";
                if (GUI.Button(new Rect(22 + (i % columns) * 106, 42 + (i / columns) * 34, 100, 28), shortcut + label))
                { _automatic = false; Select(i); }
            }
            _automatic = GUI.Toggle(new Rect(22, 42 + rows * 34, 200, 22), _automatic, "Auto cycle / 自动轮播");
        }
    }
}
