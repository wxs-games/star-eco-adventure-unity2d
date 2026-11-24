using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarEco.Player
{
    [DefaultExecutionOrder(30)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController), typeof(Animator))]
    public sealed class Player3DGameplayVisual : MonoBehaviour
    {
        [Serializable]
        private struct Motion
        {
            public string State;
            public AnimationClip Clip;
            public bool Grip;
            public bool LeftHandTool;
            public bool ShowHeldTool;
        }

        [SerializeField] private GameObject _modelPrefab;
        [SerializeField] private AnimationClip _jumpPreparationClip;
        [SerializeField] private GameObject _heldToolPrefab;
        [SerializeField] private GameObject _sprayerPrefab;
        [SerializeField] private Mesh _swingSprayerMesh;
        [SerializeField] private GameObject _purifyPrunerPrefab;
        private Transform _purifyPruner, _prunerRearGrip, _prunerFrontGrip;
        private MeshRenderer[] _prunerRenderers;
        [SerializeField] private AnimationClip _sprayLoop;
        [SerializeField] private AnimationClip _airKickClip;
        [SerializeField] private AnimationClip _airKickLeftClip;
        [SerializeField] private AnimationClip _runAttackClip;
        [SerializeField] private Motion[] _motions;
        private readonly Dictionary<int, Motion> _lookup = new Dictionary<int, Motion>();
        private PlayerController _player;
        private Animator _animator;
        private SpriteRenderer _sprite, _sourceTool, _visibleTool;
        private GameObject _model;
        private GameObject _heldTool;
        private MeshRenderer[] _heldToolRenderers;
        private SkinnedMeshRenderer[] _renderers;
        private Transform _leftHand, _rightHand, _leftIndex, _rightIndex;
        private Transform _leftKickFoot, _leftKickToe, _rightKickFoot, _rightKickToe;
        private Vector3 _standingModelPosition;
        [SerializeField, Min(0f)] private float _swingLeanDegrees = 13f;
        private Transform _sprayer, _tankOutlet;
        private Vector3 _sprayerOriginalScale;
        private MeshFilter _sprayerMeshFilter;
        private Mesh _sprayerOriginalMesh;
        private const float SwingSprayerRearShift = .146f * (SwingSprayerLengthMultiplier - 1f);
        internal const float SwingSprayerLengthMultiplier = 4.2f;
        private MeshRenderer[] _sprayerRenderers;
        private LineRenderer _hose;
        private ParticleSystem _sprayMist;
        private ParticleSystemRenderer _mistRenderer;
        private Material _sprayMaterial;
        private Material _mistMaterial;
        private Texture2D _mistTexture;
        private Transform[] _locomotionLegs;
        private Vector3[] _walkingLegPositions;
        private Quaternion[] _walkingLegRotations;
        private float _sprayTime, _sprayWalkTime, _mistCount;
        private MaterialPropertyBlock _properties;
        private static readonly int Tint = Shader.PropertyToID("_Color");
        private static readonly int Run = Animator.StringToHash("Run");
        private static readonly int Attack = Animator.StringToHash("Attack");
        private static readonly int Attack02 = Animator.StringToHash("Attack02");
        private static readonly int Idle = Animator.StringToHash("Idle");
        private static readonly int Jump = Animator.StringToHash("Jump");
        private static readonly int Fall = Animator.StringToHash("Fall");
        private static readonly int Crouch = Animator.StringToHash("Crouch");
        private static readonly int WallJump = Animator.StringToHash("WallJump");
        private static readonly int WallSlide = Animator.StringToHash("WallSlide");
        private static readonly int LedgeGrab = Animator.StringToHash("LedgeGrab");
        private static readonly int Attack03 = Animator.StringToHash("Attack03");
        private static readonly int ItemGetHold = Animator.StringToHash("ItemGetHold");
        private static readonly int Death = Animator.StringToHash("Death");
        private static readonly int Victory = Animator.StringToHash("Victory");
        private const float MaxJumpPreparationDuration = .06f;
        private int _previousState;
        private float _loopTime, _blinkTime;
        private float _runCycleTime;
        private bool CanShowModel => _model != null && _sprite.enabled;

        internal bool HasSprayVisual => isActiveAndEnabled && _sprayer != null && _sprite.enabled;

        internal bool CanOpenFirstSwingHitbox()
        {
            if (!isActiveAndEnabled || !CanShowModel || !_player.IsGrounded
                || Mathf.Abs(_player.Body.velocity.x) > .65f)
                return true;

            var state = _animator.IsInTransition(0)
                ? _animator.GetNextAnimatorStateInfo(0)
                : _animator.GetCurrentAnimatorStateInfo(0);
            // The standing mocap reaches the target around halfway through the state.
            return state.shortNameHash == Attack && state.normalizedTime >= .5f;
        }

        internal float JumpPreparationDuration => isActiveAndEnabled && CanShowModel && _jumpPreparationClip != null
            ? Mathf.Min(_jumpPreparationClip.length, MaxJumpPreparationDuration)
            : 0f;

        internal bool IsDeathAnimationPending
        {
            get
            {
                if (!isActiveAndEnabled || _model == null || !_sprite.enabled
                    || !_animator.isActiveAndEnabled || !_lookup.ContainsKey(Death))
                    return false;

                var state = _animator.GetCurrentAnimatorStateInfo(0);
                return state.shortNameHash != Death || state.normalizedTime < 1f;
            }
        }

        private void Awake()
        {
            if (_modelPrefab == null || _motions == null) return;
            _properties = new MaterialPropertyBlock();
            _player = GetComponent<PlayerController>();
            _sprite = GetComponent<SpriteRenderer>();
            _sprite.forceRenderingOff = true;
            _animator = GetComponent<Animator>();
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            CacheMotions();

            _model = Instantiate(_modelPrefab, transform, false);
            _model.name = "Player 3D Visual";
            if (_model.TryGetComponent<Animator>(out var visualAnimator))
                visualAnimator.enabled = false;
            PlaceStandingModel(_model.transform);
            _standingModelPosition = _model.transform.localPosition;
            _renderers = _model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            CacheLocomotionLegs();
            var bones = _model.GetComponentsInChildren<Transform>(true);
            _leftHand = FindBone(bones, "LeftHand");
            _rightHand = FindBone(bones, "RightHand");
            _leftIndex = FindBone(bones, "LeftHandIndex1");
            _rightIndex = FindBone(bones, "RightHandIndex1");
            _leftKickFoot = FindBone(bones, "LeftFoot");
            _leftKickToe = FindBone(bones, "LeftToeBase");
            _rightKickFoot = FindBone(bones, "RightFoot");
            _rightKickToe = FindBone(bones, "RightToeBase");
            foreach (var renderer in _renderers)
                renderer.updateWhenOffscreen = true;
            if (_sprayerPrefab != null && _sprayLoop != null && _leftHand != null && _rightHand != null)
                CreateSprayer();
            var anchor = GetComponentInChildren<PlayerToolAnchor>(true);
            if (anchor != null && anchor.ToolRenderer != null)
            {
                _sourceTool = anchor.ToolRenderer;
                _sourceTool.forceRenderingOff = true;
                if (_heldToolPrefab != null)
                {
                    _heldTool = Instantiate(_heldToolPrefab, _model.transform, false);
                    _heldTool.name = "Held 3D Pliers";
                    _heldToolRenderers = _heldTool.GetComponentsInChildren<MeshRenderer>(true);
                    _heldTool.SetActive(false);
                }
                else
                {
                    _visibleTool = new GameObject("Held tool visual").AddComponent<SpriteRenderer>();
                    _visibleTool.transform.SetParent(transform, false);
                    _visibleTool.sharedMaterial = _sourceTool.sharedMaterial;
                    _visibleTool.enabled = false;
                }
            }
            _model.SetActive(false);
        }

        internal Vector2 AirKickImpactPosition(Vector2 fallback)
        {
            bool left = _sprite != null && _sprite.flipX && _airKickLeftClip != null;
            Transform foot = left ? _leftKickFoot : _rightKickFoot;
            Transform toe = left ? _leftKickToe : _rightKickToe;
            if (toe == null || _model == null || !_model.activeInHierarchy || AirKickClip == null)
                return fallback;

            // Physics ticks can reach contact before the render frame has sampled the extended boot.
            SampleAirKick();
            return foot != null ? Vector3.Lerp(foot.position, toe.position, .18f) : toe.position;
        }

        private AnimationClip AirKickClip =>
            _sprite.flipX && _airKickLeftClip != null ? _airKickLeftClip : _airKickClip;

        private void SampleAirKick()
        {
            var clip = AirKickClip;
            // Both clips are authored for the side camera, with mirrored poses.
            _model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            float time = Mathf.Max(0f, _player.AirKickElapsed);
            if (time > PlayerAttackState.AirKickImpact)
            {
                float recovery = Mathf.InverseLerp(PlayerAttackState.AirKickImpact, PlayerAttackState.AirKickDuration, time);
                time = Mathf.Lerp(PlayerAttackState.AirKickImpact, clip.length, recovery);
            }
            clip.SampleAnimation(_model, time);
        }

        private void LateUpdate()
        {
            if (_model == null) return;
            if (_properties == null)
                _properties = new MaterialPropertyBlock();
            if (_lookup.Count == 0)
                CacheMotions();
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            if (_animator.IsInTransition(0))
            {
                var next = _animator.GetNextAnimatorStateInfo(0);
                if (next.shortNameHash != 0) state = next;
            }
            float preparationTime = _player.JumpPreparationTime;
            bool defeated = state.shortNameHash == Death;
            bool celebrating = state.shortNameHash == Victory;
            bool kicking = !defeated && !celebrating && AirKickClip != null && _player.AirKickElapsed >= 0f;
            bool spraying = !defeated && !celebrating && !kicking && HasSprayVisual
                && _player.Spray != null && _player.Spray.IsSpraying;
            bool preparing = !defeated && !celebrating && !kicking && !spraying
                && preparationTime >= 0f && _jumpPreparationClip != null;
            bool showingItem = state.shortNameHash == ItemGetHold;
            bool groundAttack = state.shortNameHash == Attack || state.shortNameHash == Attack02
                || state.shortNameHash == Attack03;
            float speed = Mathf.Abs(_player.Body.velocity.x);
            if (!preparing && !kicking && !spraying && (state.shortNameHash == Run || groundAttack)
                && _player.IsGrounded && speed > .08f)
                _runCycleTime += Time.deltaTime * Mathf.Clamp(speed / 5f, .6f, 1.35f);
            else
                _runCycleTime = 0f;

            if (!CanShowModel)
            {
                UpdateSprayer(false);
                HideModel();
                _previousState = 0;
                return;
            }
            _model.SetActive(true);
            _sprite.forceRenderingOff = true;
            _model.transform.localPosition = _standingModelPosition;
            _model.transform.localRotation = ModelFacing(_sprite.flipX);

            var motion = default(Motion);
            if (kicking)
            {
                _previousState = 0;
                motion = default;
                SampleAirKick();
            }
            else if (spraying)
            {
                _previousState = 0;
                SampleSpray();
                _sprayTime = Mathf.Repeat(_sprayTime + Time.deltaTime, _sprayLoop.length);
            }
            else if (preparing)
            {
                _previousState = 0;
                motion = new Motion { Clip = _jumpPreparationClip };
                float progress = Mathf.InverseLerp(0f, JumpPreparationDuration, preparationTime);
                motion.Clip.SampleAnimation(_model, progress * motion.Clip.length);
            }
            else
            {
                motion = ResolveMotion(state.shortNameHash);
                if (motion.Clip != null)
                {
                    float legWeight = groundAttack && _player.IsGrounded
                        ? Mathf.InverseLerp(.08f, .65f, speed) : 0f;
                    bool overlayLegs = _lookup.TryGetValue(Run, out var run) && legWeight > 0f;
                    if (overlayLegs)
                        CaptureLocomotionLegs(run.Clip, _runCycleTime);
                    SampleState(state, motion.Clip);
                    if (overlayLegs)
                        ApplyLocomotionLegs(legWeight);
                }
            }

            _blinkTime += Time.deltaTime;
            float blinkPhase = _blinkTime % 3.2f;
            float blink = blinkPhase < .18f ? Mathf.Sin(blinkPhase / .18f * Mathf.PI) * 100f : 0f;
            if (defeated)
                blink = 100f * Mathf.InverseLerp(.65f, .86f, state.normalizedTime);
            _properties.SetColor(Tint, _sprite.color);
            bool grip = spraying || motion.Grip;
            if (!preparing && !kicking && !spraying && state.shortNameHash == Attack03)
            {
                grip = grip && motion.Clip != null
                    && state.normalizedTime * motion.Clip.length < PlayerAttackState.ThrowReleaseTime
                    && (_player.ToolThrow == null || _player.ToolThrow.ToolInHand);
            }
            foreach (var renderer in _renderers)
            {
                renderer.sortingLayerID = _sprite.sortingLayerID;
                renderer.sortingOrder = _sprite.sortingOrder;
                renderer.SetPropertyBlock(_properties);
                var mesh = renderer.sharedMesh;
                if (mesh == null) continue;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string name = mesh.GetBlendShapeName(i);
                    if (name.Contains("Blink_")) renderer.SetBlendShapeWeight(i, blink);
                    else if (name.Contains("RunGrip")) renderer.SetBlendShapeWeight(i, grip ? 100f : 0f);
                }
            }
            bool swingingSprayer = _sprayer != null && !preparing && !kicking && !spraying
                && (state.shortNameHash == Attack || state.shortNameHash == Attack02);
            if (swingingSprayer && state.shortNameHash == Attack02 && _player.IsGrounded)
            {
                float phase = Mathf.Clamp01(state.normalizedTime);
                float reach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.04f, .34f, phase))
                    * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.53f, .86f, phase)));
                float facing = _sprite.flipX ? -1f : 1f;
                _model.transform.localRotation = Quaternion.AngleAxis(-facing * _swingLeanDegrees * reach, Vector3.forward)
                    * _model.transform.localRotation;
            }
            bool twoHandSwing = swingingSprayer && state.shortNameHash == Attack && _player.IsGrounded
                && motion.Clip != null && motion.Clip != _runAttackClip;
            Transform swingHand = swingingSprayer && !twoHandSwing
                ? (motion.LeftHandTool ? _leftHand : _rightHand) : null;
            Transform toolHand = showingItem || defeated || celebrating || kicking || spraying || swingingSprayer
                ? null
                : motion.LeftHandTool ? _leftHand : _rightHand;
            UpdateTool(toolHand, motion.ShowHeldTool);
            UpdateSprayer(spraying, swingHand, twoHandSwing, state.shortNameHash == Attack02);
        }

        private void CacheMotions()
        {
            foreach (Motion motion in _motions)
                if (motion.Clip != null)
                    _lookup[Animator.StringToHash(motion.State)] = motion;
        }

        private Motion ResolveMotion(int state)
        {
            if (_player.Spray != null && _player.Spray.ChargeHeld
                && (state == Idle || state == Crouch || !_lookup.ContainsKey(state))
                && _lookup.TryGetValue(Crouch, out var chargePose))
                return chargePose;
            if (_lookup.TryGetValue(state, out var motion))
            {
                if (state == Attack && _runAttackClip != null && _player.IsGrounded
                    && Mathf.Abs(_player.Body.velocity.x) > .65f)
                    motion.Clip = _runAttackClip;
                return motion;
            }

            int fallback = state == WallJump ? Jump
                : state == WallSlide || state == LedgeGrab ? Fall : Idle;
            return _lookup.TryGetValue(fallback, out motion) ? motion : _lookup.GetValueOrDefault(Idle);
        }

        private void CreateSprayer()
        {
            if (_purifyPrunerPrefab != null)
            {
                _purifyPruner = Instantiate(_purifyPrunerPrefab, _model.transform, false).transform;
                var parts = _purifyPruner.GetComponentsInChildren<Transform>(true);
                _prunerRearGrip = FindBone(parts, "RearGrip");
                _prunerFrontGrip = FindBone(parts, "FrontGrip");
                SlimPrunerGrip(_prunerRearGrip);
                SlimPrunerGrip(_prunerFrontGrip);
                _prunerRenderers = _purifyPruner.GetComponentsInChildren<MeshRenderer>(true);
                _purifyPruner.gameObject.SetActive(false);
            }
            _sprayer = Instantiate(_sprayerPrefab, _model.transform, false).transform;
            _sprayerOriginalScale = _sprayer.localScale;
            _sprayerMeshFilter = _sprayer.GetComponentInChildren<MeshFilter>(true);
            if (_sprayerMeshFilter != null) _sprayerOriginalMesh = _sprayerMeshFilter.sharedMesh;
            _sprayerRenderers = _sprayer.GetComponentsInChildren<MeshRenderer>();
            _sprayer.gameObject.SetActive(false);
            _tankOutlet = new GameObject("Tank Hose Outlet").transform;
            var bones = _model.GetComponentsInChildren<Transform>();
            var chest = FindBone(bones, "Spine2");
            _tankOutlet.SetParent(chest != null ? chest : _model.transform, false);
            _tankOutlet.position = _model.transform.TransformPoint(new Vector3(-.18f, .04f, -.15f));
            _sprayMaterial = new Material(Shader.Find("Sprites/Default"));
            _hose = new GameObject("Sprayer Hose").AddComponent<LineRenderer>();
            _hose.transform.SetParent(_model.transform, false);
            _hose.sharedMaterial = _sprayMaterial;
            _hose.positionCount = 16;
            _hose.numCapVertices = 4;
            _hose.startColor = _hose.endColor = new Color(.07f, .31f, .54f);
            _hose.enabled = false;

            _sprayMist = new GameObject("Sprayer Mist").AddComponent<ParticleSystem>();
            _sprayMist.transform.SetParent(transform, false);
            _sprayMist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _sprayMist.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            var emission = _sprayMist.emission;
            emission.enabled = false;
            _mistRenderer = _sprayMist.GetComponent<ParticleSystemRenderer>();
            _mistTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float radius = new Vector2(x - 7.5f, y - 7.5f).magnitude / 7.5f;
                _mistTexture.SetPixel(x, y, new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, radius)));
            }
            _mistTexture.Apply();
            _mistMaterial = new Material(_sprayMaterial) { mainTexture = _mistTexture };
            _mistRenderer.sharedMaterial = _mistMaterial;
            _sprayMist.Play();
        }

        private static void SlimPrunerGrip(Transform grip)
        {
            if (grip != null)
                grip.localScale = Vector3.Scale(grip.localScale, new Vector3(1f, .64f, .64f));
        }

        private void SampleSpray()
        {
            float speed = Mathf.Abs(_player.Body.velocity.x);
            bool walking = speed > .08f && _lookup.TryGetValue(Run, out _);
            if (walking)
            {
                var run = _lookup[Run].Clip;
                _sprayWalkTime = Mathf.Repeat(_sprayWalkTime + Time.deltaTime * Mathf.Clamp(speed / 5f, .45f, .9f), run.length);
                CaptureLocomotionLegs(run, _sprayWalkTime);
            }

            _sprayLoop.SampleAnimation(_model, _sprayTime);
            if (!walking) return;
            ApplyLocomotionLegs(Mathf.InverseLerp(.08f, 1.5f, speed));
        }

        private void CacheLocomotionLegs()
        {
            var bones = _model.GetComponentsInChildren<Transform>(true);
            var legs = new List<Transform>(8);
            foreach (string side in new[] { "Left", "Right" })
            foreach (string joint in new[] { "UpLeg", "Leg", "Foot", "ToeBase" })
            {
                var bone = FindBone(bones, side + joint);
                if (bone != null) legs.Add(bone);
            }
            _locomotionLegs = legs.ToArray();
            _walkingLegPositions = new Vector3[_locomotionLegs.Length];
            _walkingLegRotations = new Quaternion[_locomotionLegs.Length];
        }

        private static Transform FindBone(Transform[] bones, string name)
        {
            return Array.Find(bones, bone => bone.name == name
                || bone.name.EndsWith(":" + name, StringComparison.Ordinal));
        }

        private Vector3 PalmAxis(Transform hand)
        {
            Transform index = hand == _leftHand ? _leftIndex : _rightIndex;
            return index != null ? (index.position - hand.position).normalized : hand.up;
        }

        private void CaptureLocomotionLegs(AnimationClip run, float time)
        {
            if (_walkingLegPositions == null || _walkingLegRotations == null)
                CacheLocomotionLegs();
            run.SampleAnimation(_model, time % run.length);
            for (int i = 0; i < _locomotionLegs.Length; i++)
            {
                _walkingLegPositions[i] = _locomotionLegs[i].localPosition;
                _walkingLegRotations[i] = _locomotionLegs[i].localRotation;
            }
        }

        private void ApplyLocomotionLegs(float weight)
        {
            for (int i = 0; i < _locomotionLegs.Length; i++)
            {
                var leg = _locomotionLegs[i];
                leg.localPosition = Vector3.Lerp(leg.localPosition, _walkingLegPositions[i], weight);
                leg.localRotation = Quaternion.Slerp(leg.localRotation, _walkingLegRotations[i], weight);
            }
        }

        private void UpdateSprayer(bool spraying, Transform swingHand = null, bool twoHandSwing = false,
            bool secondSwing = false)
        {
            if (_sprayer == null) return;
            bool visible = spraying || swingHand != null || twoHandSwing;
            var weaponScale = _sprayerOriginalScale;
            bool swinging = swingHand != null || twoHandSwing;
            bool usingPruner = swinging && _prunerRearGrip != null && _prunerFrontGrip != null;
            if (_purifyPruner != null) _purifyPruner.gameObject.SetActive(usingPruner);
            bool useSwingMesh = swinging && _swingSprayerMesh != null && _sprayerMeshFilter != null;
            if (_sprayerMeshFilter != null)
                _sprayerMeshFilter.sharedMesh = useSwingMesh ? _swingSprayerMesh : _sprayerOriginalMesh;
            if (swinging && !useSwingMesh)
                weaponScale.z *= SwingSprayerLengthMultiplier;
            _sprayer.localScale = weaponScale;
            _sprayer.gameObject.SetActive(visible && !usingPruner);
            _hose.enabled = visible && !usingPruner;
            if (!spraying)
            {
                _sprayTime = 0f;
                _sprayWalkTime = 0f;
                _mistCount = 0f;
            }
            if (!visible) return;

            float scale = _model.transform.lossyScale.y;
            if (swingHand != null)
            {
                PositionSwingSprayer(swingHand, scale, secondSwing);
            }
            else
            {
                Vector3 rear = _rightHand.position + PalmAxis(_rightHand) * (.047f * scale);
                Vector3 front = _leftHand.position + PalmAxis(_leftHand) * (.047f * scale);
                Vector3 gripCenter = (rear + front) * .5f + Vector3.up * (.055f * scale);
                _sprayer.SetPositionAndRotation(gripCenter, Quaternion.LookRotation(front - rear, Vector3.up));
            }
            if (usingPruner)
            {
                Vector3 sourceAxis = _purifyPruner.InverseTransformVector(_prunerFrontGrip.position - _prunerRearGrip.position);
                _purifyPruner.rotation = _sprayer.rotation * Quaternion.FromToRotation(sourceAxis, Vector3.forward);
                Vector3 anchor = swingHand != null
                    ? swingHand.position + PalmAxis(swingHand) * (.047f * scale)
                    : _sprayer.position - Vector3.up * (.055f * scale);
                // The standing motion has a close two-handed hold on the rear sleeve.
                // The camera is on the negative Z side. Give the fingers room in front of the sleeve.
                anchor += Vector3.forward * (.085f * scale);
                _purifyPruner.position += anchor - _prunerRearGrip.position;
                foreach (var renderer in _prunerRenderers)
                {
                    renderer.sortingLayerID = _sprite.sortingLayerID;
                    renderer.sortingOrder = _sprite.sortingOrder;
                }
                return;
            }
            foreach (var renderer in _sprayerRenderers)
            {
                renderer.sortingLayerID = _sprite.sortingLayerID;
                renderer.sortingOrder = _sprite.sortingOrder + (swingHand != null || twoHandSwing ? 1 : 0);
            }

            Vector3 end = _sprayer.TransformPoint(new Vector3(0f, 0f, -.146f - (useSwingMesh ? SwingSprayerRearShift : 0f)));
            Vector3 outward = _model.transform.right * (-.16f * scale);
            Vector3 sag = Vector3.down * (.19f * scale);
            _hose.widthMultiplier = .026f * scale;
            _hose.sortingLayerID = _sprite.sortingLayerID;
            _hose.sortingOrder = _sprite.sortingOrder - 1;
            for (int i = 0; i < _hose.positionCount; i++)
            {
                float t = i / (float)(_hose.positionCount - 1);
                _hose.SetPosition(i, Vector3.Lerp(_tankOutlet.position, end, t)
                    + (outward + sag) * (4f * t * (1f - t)));
            }

            if (!spraying) return;

            _mistRenderer.sortingLayerID = _sprite.sortingLayerID;
            _mistRenderer.sortingOrder = _sprite.sortingOrder + 1;
            var mist = new ParticleSystem.EmitParams
            {
                position = _sprayer.TransformPoint(new Vector3(0f, -.16f, .235f)),
                startColor = new Color(.67f, .94f, 1f, .48f),
                startLifetime = .22f
            };
            _mistCount += Time.deltaTime * 95f;
            while (_mistCount >= 1f)
            {
                _mistCount -= 1f;
                mist.velocity = (_sprayer.forward * 8.6f + Vector3.down * 1.7f
                    + UnityEngine.Random.insideUnitSphere * .55f) * scale;
                mist.startSize = UnityEngine.Random.Range(.04f, .075f) * scale;
                _sprayMist.Emit(mist, 1);
            }
        }

        private void PositionSwingSprayer(Transform hand, float scale, bool secondSwing)
        {
            Vector3 palm = PalmAxis(hand);
            Vector3 forearm = Vector3.ProjectOnPlane(hand.position - hand.parent.position, Vector3.forward).normalized;
            Vector3 facing = _sprite.flipX ? Vector3.left : Vector3.right;
            Vector3 shaft = (forearm + facing * .45f).normalized;
            Vector3 up = Mathf.Abs(Vector3.Dot(shaft, Vector3.up)) > .9f ? Vector3.back : Vector3.up;
            Vector3 grip = hand.position + palm * (.047f * scale);
            if (secondSwing)
            {
                // Keep the long shaft in the gameplay plane, with a consistent roll through the sweep.
                shaft = (facing * (.65f + Mathf.Abs(forearm.x)) + Vector3.up * forearm.y).normalized;
                up = new Vector3(-shaft.y * facing.x, shaft.x * facing.x, 0f);
                _sprayer.SetPositionAndRotation(grip, Quaternion.LookRotation(shaft, up));
                // Anchor the handle near the hose coupling so the extra length extends ahead of the hand.
                float gripZ = _sprayer.localScale.z == _sprayerOriginalScale.z ? -.11f - SwingSprayerRearShift : -.11f;
                _sprayer.position += grip - _sprayer.TransformPoint(new Vector3(0f, 0f, gripZ));
                return;
            }
            // The model origin is at the middle of the handle; the hand sits closer to the hose end.
            _sprayer.SetPositionAndRotation(grip + shaft * (.09f * scale), Quaternion.LookRotation(shaft, up));
        }

        private void OnDestroy()
        {
            if (_sprayMaterial != null) Destroy(_sprayMaterial);
            if (_mistMaterial != null) Destroy(_mistMaterial);
            if (_mistTexture != null) Destroy(_mistTexture);
        }

        private void SampleState(AnimatorStateInfo state, AnimationClip clip)
        {
            if (_previousState != state.shortNameHash) _loopTime = 0f;
            _previousState = state.shortNameHash;
            float time = Mathf.Clamp01(state.normalizedTime) * clip.length;
            if (state.shortNameHash == Run)
            {
                time = _runCycleTime % clip.length;
            }
            else if (state.shortNameHash == Victory)
            {
                time = Mathf.Min(_loopTime, clip.length);
                _loopTime += Time.deltaTime;
            }
            else if (clip.isLooping)
            {
                time = Mathf.Repeat(state.normalizedTime, 1f) * clip.length;
            }
            clip.SampleAnimation(_model, time);
        }

        internal void PlaceStandingModel(Transform model)
        {
            if (!TryGetComponent<BoxCollider2D>(out var box)) return;
            model.localScale = Vector3.one * (box.size.y / 1.8f);
            // The existing sprite's standing baseline is below its physics box.
            model.localPosition = new Vector3(0f, box.offset.y - .15f, 0f);
        }

        internal static Quaternion ModelFacing(bool left) => Quaternion.Euler(0f, left ? -114f : 114f, 0f);

#if UNITY_EDITOR
        internal bool TryGetPreviewAssets(out GameObject prefab, out AnimationClip idle)
        {
            prefab = _modelPrefab;
            idle = null;
            if (prefab == null || _motions == null) return false;
            foreach (var motion in _motions)
            {
                if (motion.State != "Idle" || motion.Clip == null) continue;
                idle = motion.Clip;
                return true;
            }
            return false;
        }
#endif

        private void UpdateTool(Transform hand, bool showHeldTool)
        {
            if (_sourceTool == null) return;
            _sourceTool.forceRenderingOff = true;
            bool show = hand != null && _sourceTool.gameObject.activeInHierarchy && _sourceTool.enabled;
            if (_heldTool != null)
            {
                // Some legacy sprite clips already contain the prop and hide ToolAnchor.
                bool showModel = hand != null && _sourceTool.enabled
                    && (showHeldTool || _sourceTool.gameObject.activeInHierarchy);
                _heldTool.SetActive(showModel);
                if (hand != null) UpdateHeldModel(hand, showModel);
                return;
            }
            if (_visibleTool == null) return;
            _visibleTool.enabled = show;
            if (!show) return;
            _visibleTool.sprite = _sourceTool.sprite;
            _visibleTool.color = _sourceTool.color;
            _visibleTool.flipX = _sourceTool.flipX;
            _visibleTool.sortingLayerID = _sourceTool.sortingLayerID;
            _visibleTool.sortingOrder = _sourceTool.sortingOrder;
            _visibleTool.transform.localScale = _sourceTool.transform.localScale;
            _visibleTool.transform.localRotation = _sourceTool.transform.localRotation;
            var position = hand.position + PalmAxis(hand) * .035f;
            position.z = transform.position.z - .36f;
            _visibleTool.transform.position = position;
        }

        internal bool TryGetHeldTool(out GameObject prefab, out Transform grip)
        {
            if (isActiveAndEnabled && _heldTool != null && _heldTool.activeInHierarchy)
            {
                prefab = _heldToolPrefab;
                grip = _heldTool.transform;
                return true;
            }
            prefab = null;
            grip = null;
            return false;
        }

        internal bool TryGetItemHoldPoint(out Vector3 point)
        {
            point = default;
            if (_previousState != ItemGetHold || _model == null || !_model.activeInHierarchy
                || _leftHand == null || _rightHand == null)
            {
                return false;
            }

            float handLength = .065f * _model.transform.lossyScale.y;
            point = (_leftHand.position + PalmAxis(_leftHand) * handLength
                   + _rightHand.position + PalmAxis(_rightHand) * handLength) * .5f;
            point.z = transform.position.z - .36f;
            return true;
        }

        private void UpdateHeldModel(Transform hand, bool visible)
        {
            Vector3 toolAxis = PalmAxis(hand);
            // Keep the wide face readable in the side view while the grip follows the hand.
            Vector3 face = Vector3.ProjectOnPlane(Vector3.back, toolAxis);
            if (face.sqrMagnitude < .01f) face = hand.forward;
            Vector3 grip = hand.position + toolAxis * (.045f * _model.transform.lossyScale.y);
            _heldTool.transform.SetPositionAndRotation(grip, Quaternion.LookRotation(face, toolAxis));
            if (!visible) return;
            _properties.SetColor(Tint, _sourceTool.color);
            foreach (var renderer in _heldToolRenderers)
            {
                renderer.sortingLayerID = _sprite.sortingLayerID;
                renderer.sortingOrder = _sprite.sortingOrder + 1;
                renderer.SetPropertyBlock(_properties);
            }
        }

        private void HideModel()
        {
            if (_model != null) _model.SetActive(false);
            if (_visibleTool != null) _visibleTool.enabled = false;
        }

        private void OnDisable()
        {
            UpdateSprayer(false);
            HideModel();
        }
    }
}
