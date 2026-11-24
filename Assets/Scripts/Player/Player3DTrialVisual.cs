using System.Collections.Generic;
using UnityEngine;

namespace StarEco.Player
{
    /// <summary>Isolated 3D visual prototype. Existing 2D controller, animation events and physics remain authoritative.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public sealed class Player3DTrialVisual : MonoBehaviour
    {
        private PlayerController _player;
        private Animator _animator;
        private Transform _visual, _torso, _leftLeg, _rightLeg, _leftArm, _rightArm;
        private SpriteRenderer[] _sprites;
        private bool[] _spriteEnabled;
        private readonly List<Material> _materials = new List<Material>();
        private float _phase;
        private Vector3 _basePosition;

        private void Start()
        {
            _player = GetComponent<PlayerController>();
            _animator = GetComponent<Animator>();
            _sprites = GetComponentsInChildren<SpriteRenderer>(true);
            _spriteEnabled = new bool[_sprites.Length];
            for (int i = 0; i < _sprites.Length; i++) _spriteEnabled[i] = _sprites[i].enabled;
            Build();
        }

        private Material Paint(Color color)
        {
            var material = new Material(Shader.Find("StarEco/TrialCharacter"));
            material.color = color;
            _materials.Add(material);
            return material;
        }

        private Transform Joint(string label, Transform parent, Vector3 position)
        {
            var joint = new GameObject(label).transform;
            joint.SetParent(parent, false);
            joint.localPosition = position;
            return joint;
        }

        private void Part(string label, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = label;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = _player.Sprite.sortingLayerID;
            renderer.sortingOrder = _player.Sprite.sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void Build()
        {
            var orange = Paint(new Color(1f, .37f, .045f));
            var green = Paint(new Color(.26f, .47f, .23f));
            var cloth = Paint(new Color(.10f, .25f, .27f));
            var skin = Paint(new Color(.96f, .73f, .52f));
            var brown = Paint(new Color(.23f, .13f, .075f));
            var blue = Paint(new Color(.07f, .46f, .65f));
            var metal = Paint(new Color(.55f, .63f, .63f));
            var dark = Paint(new Color(.075f, .055f, .04f));
            _visual = Joint("Player3D_VisualTrial", transform, Vector3.zero);
            var box = GetComponent<BoxCollider2D>();
            float feet = box != null ? box.offset.y - box.size.y * .5f : -1f;
            _basePosition = new Vector3(0f, feet - .15f, -.1f);
            _visual.localPosition = _basePosition;
            _torso = Joint("BodyPivot", _visual, new Vector3(0f, .9f, 0f));
            Part("Vest", _torso, PrimitiveType.Capsule, new Vector3(0f, .20f, 0f), new Vector3(.46f, .34f, .35f), green);
            Part("Head", _torso, PrimitiveType.Sphere, new Vector3(.05f, .76f, 0f), new Vector3(.50f, .51f, .45f), skin);
            Part("Hair", _torso, PrimitiveType.Sphere, new Vector3(-.025f, .86f, .045f), new Vector3(.53f, .40f, .47f), brown);
            Part("Face", _torso, PrimitiveType.Sphere, new Vector3(.115f, .735f, -.07f), new Vector3(.40f, .35f, .39f), skin);
            Part("Nose", _torso, PrimitiveType.Sphere, new Vector3(.30f, .74f, -.04f), new Vector3(.12f, .12f, .15f), skin);
            Part("Eye", _torso, PrimitiveType.Sphere, new Vector3(.20f, .80f, -.226f), new Vector3(.055f, .085f, .025f), dark);
            Part("OrangeBeanie", _torso, PrimitiveType.Sphere, new Vector3(-.015f, 1.02f, 0f), new Vector3(.57f, .36f, .52f), orange);
            Part("BeanieBand", _torso, PrimitiveType.Cube, new Vector3(.01f, .94f, -.005f), new Vector3(.57f, .10f, .49f), orange);
            Part("WaterTank", _torso, PrimitiveType.Capsule, new Vector3(-.30f, .26f, .08f), new Vector3(.31f, .34f, .32f), blue);
            Part("TankCap", _torso, PrimitiveType.Cube, new Vector3(-.30f, .61f, .08f), new Vector3(.18f, .09f, .20f), metal);
            Part("Harness", _torso, PrimitiveType.Cube, new Vector3(-.13f, .29f, -.205f), new Vector3(.075f, .55f, .04f), orange);
            _leftLeg = Joint("FarHip", _visual, new Vector3(0f, .88f, .12f));
            _rightLeg = Joint("NearHip", _visual, new Vector3(0f, .88f, -.12f));
            foreach (var leg in new[] { _leftLeg, _rightLeg })
            {
                Part("Trousers", leg, PrimitiveType.Capsule, new Vector3(0f, -.34f, 0f), new Vector3(.18f, .27f, .19f), cloth);
                Part("Boot", leg, PrimitiveType.Cube, new Vector3(.07f, -.76f, 0f), new Vector3(.32f, .21f, .24f), brown);
            }
            _leftArm = Joint("FarShoulder", _torso, new Vector3(.02f, .43f, .23f));
            _rightArm = Joint("NearShoulder", _torso, new Vector3(.02f, .43f, -.23f));
            foreach (var arm in new[] { _leftArm, _rightArm })
            {
                Part("Sleeve", arm, PrimitiveType.Capsule, new Vector3(0f, -.15f, 0f), new Vector3(.18f, .15f, .18f), orange);
                Part("Forearm", arm, PrimitiveType.Capsule, new Vector3(.02f, -.37f, 0f), new Vector3(.13f, .14f, .14f), skin);
                Part("Glove", arm, PrimitiveType.Sphere, new Vector3(.04f, -.52f, 0f), new Vector3(.18f, .19f, .18f), brown);
            }
            Part("ToolHandle", _rightArm, PrimitiveType.Cube, new Vector3(.07f, -.67f, 0f), new Vector3(.08f, .31f, .08f), metal);
            Part("ToolJawA", _rightArm, PrimitiveType.Cube, new Vector3(.01f, -.85f, 0f), new Vector3(.08f, .16f, .10f), metal);
            Part("ToolJawB", _rightArm, PrimitiveType.Cube, new Vector3(.14f, -.85f, 0f), new Vector3(.08f, .16f, .10f), metal);
        }

        private void LateUpdate()
        {
            if (_visual == null || _player == null) return;
            foreach (var sprite in _sprites) if (sprite != null) sprite.enabled = false;
            float speed = Mathf.Abs(_player.Body.velocity.x);
            bool grounded = _player.IsGrounded;
            _phase += Time.deltaTime * Mathf.Lerp(0f, 13f, Mathf.Clamp01(speed / 5f));
            float gait = grounded ? Mathf.Sin(_phase) * Mathf.Clamp01(speed) * 30f : 0f;
            float legLeft = grounded ? gait : -22f;
            float legRight = grounded ? -gait : 28f;
            float armLeft = -gait * .7f;
            float armRight = gait * .7f;
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            bool attack = state.IsName("Attack") || state.IsName("Attack2") || state.IsName("Attack3") || state.IsName("AttackAir");
            if (attack) armRight = Mathf.Lerp(115f, -35f, Mathf.Clamp01(state.normalizedTime % 1f));
            if (_player.Spray != null && _player.Spray.IsSpraying) armRight = 85f;
            float blend = 1f - Mathf.Exp(-18f * Time.deltaTime);
            Pose(_leftLeg, legLeft, blend); Pose(_rightLeg, legRight, blend);
            Pose(_leftArm, armLeft, blend); Pose(_rightArm, armRight, blend);
            _visual.localScale = new Vector3(_player.FacingDir, 1f, 1f);
            _visual.localPosition = _basePosition + Vector3.up * (grounded ? Mathf.Abs(Mathf.Sin(_phase)) * Mathf.Clamp01(speed) * .025f : 0f);
        }

        private static void Pose(Transform joint, float angle, float blend)
        {
            joint.localRotation = Quaternion.Slerp(joint.localRotation, Quaternion.Euler(0f, 0f, angle), blend);
        }

        private void OnEnable()
        {
            if (_visual != null) _visual.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            if (_visual != null) _visual.gameObject.SetActive(false);
            if (_sprites != null) for (int i = 0; i < _sprites.Length; i++) if (_sprites[i] != null) _sprites[i].enabled = _spriteEnabled[i];
        }

        private void OnDestroy()
        {
            if (_sprites != null) for (int i = 0; i < _sprites.Length; i++) if (_sprites[i] != null) _sprites[i].enabled = _spriteEnabled[i];
            if (_visual != null) Destroy(_visual.gameObject);
            foreach (var material in _materials) if (material != null) Destroy(material);
        }
    }
}
