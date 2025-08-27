using System;
using Lean.Pool;
using UnityEngine;
using UnityEngine.VFX;

[Serializable]
public class TornadoMovementStats
{
    [Header("Speed")]
    [Range(-50, 50)] public float minSpeed = 2f;
    [Range(-50, 50)] public float maxSpeed = 5f;
    [Range(0, 50)] public float speedChangeRate = 0.5f;

    [Header("Perlin Noise Direction Chaos")]
    [Range(0, 50)] public float noiseScale = 1f;
    [Range(0, 50)] public float noiseSpeed = 1f;

    [Header("Sinusoidal Sway")]
    [Range(0, 50)] public float swayAmplitude;
    [Range(0, 50)] public float swayFrequency;

    [Header("Scale Time")]
    public float scaleUpTime = 1f;
    public float scaleDownTime = 1f;
}

[RequireComponent(typeof(VisualEffect))]
public class TornadoProjectile : MonoBehaviourWithGameState, IPoolable
{
    [Header("VFX")]
    [SerializeField] private VisualEffect vfx;

    [Header("Damage Collider")]
    [SerializeField] private DamageCollider damageCollider;

    [Header("Stats")]
    [SerializeField] private TornadoStats tornadoStats;
    [SerializeField] private TornadoMovementStats movementStats;

    private const float ChangeSpeedInterval = 2f;

    private float _angleDegrees, _elapsedTime;

    private bool _isScalingUp = false, _isScalingDown = false;
    private Vector3 _startUpScale, _startDownScale;
    private Vector3 _targetScaleUp, _targetScaleDown;
    private float _scalingUpTime, _scalingDownTime;
    
    private float _speedTimer;
    private float _currentSpeed, _timeOffset, _targetSpeed;

    private bool _isWobblingUp, _isWobblingDown;
    private float _wobbleTime;

    private DamageArgs damageArgs = new DamageArgs(0, DamageSource.Tornado);

    void Awake()
    {
        if (vfx == null) vfx = GetComponent<VisualEffect>();

        if (damageCollider == null) damageCollider = GetComponentInChildren<DamageCollider>();
        if (damageCollider) damageCollider.SetDamageType(IsDamageOnStay: true,
            damageInterval: tornadoStats.DamageInterval);
    }

    new void Start()
    {
        base.Start();
    }

    void Update()
    {
        if (_gameState == null || _gameState.IsPaused) return;

        TornadoDurationUpdate();
        TornadoMovementUpdate();
    }

    private void TornadoDurationUpdate()
    {
        AnimateWobbleValues();

        if (_isScalingUp)
        {
            ScaleUpUpdate();
            return;
        }

        if (_isScalingDown)
        {
            ScaleDownUpdate();
            return;
        }

        _elapsedTime += Time.deltaTime;
        if (_elapsedTime >= tornadoStats.Duration)
        {
            _startDownScale = transform.localScale;
            _isScalingDown = true;
        }
    }

    private void ScaleUpUpdate()
    {
        _scalingUpTime += Time.deltaTime;
        transform.localScale = Vector3.Lerp(_startUpScale, _targetScaleUp, _scalingUpTime);
        if (_scalingUpTime >= movementStats.scaleUpTime)
        {
            _isScalingUp = false;
        }
    }

    private void ScaleDownUpdate()
    {
        _scalingDownTime += Time.deltaTime;
        transform.localScale = Vector3.Lerp(_startDownScale, _targetScaleDown, _scalingDownTime);
        if (_scalingDownTime >= movementStats.scaleDownTime)
        {
            _isScalingDown = false;
            LeanPool.Despawn(gameObject);
        }
    }

    private void TornadoMovementUpdate()
    {
        float delta = Time.deltaTime;

        // Update speed only every few seconds
        _speedTimer += delta;
        if (_speedTimer > ChangeSpeedInterval)
        {
            _targetSpeed = UnityEngine.Random.Range(movementStats.minSpeed, movementStats.maxSpeed);
            _speedTimer = 0f;
        }

        // Smoothly interpolate to target speed, target speed changes every few seconds
        _currentSpeed = Mathf.Lerp(_currentSpeed, _targetSpeed, delta * movementStats.speedChangeRate);

        // Perlin noise is a way to generate smooth random values, Mathf.PerlinNoise returns 0 to 1
        // We remap it to -1 to +1 by multiplying by 2 and subtracting 1
        float noise = Mathf.PerlinNoise(_timeOffset, Time.time * movementStats.noiseSpeed) * 2f - 1f;
        float noiseTurn = noise * 45f; // max 45 degrees turn if noise is 1

        _angleDegrees += noiseTurn * delta;

        // Add sway to make it less linear
        // Mathf.Sin returns -1 to +1 forming a sine wave, multiple it by frequency and amplitude
        // Time.time is used to make it change over time - this will change sway's value over time forming the sine wave
        // Frequency controls how fast it oscillates, amplitude controls how wide the sway is
        float sway = Mathf.Sin(Time.time * movementStats.swayFrequency) * movementStats.swayAmplitude;
        float finalAngleDegrees = _angleDegrees + sway;

        // finalAngle is in degrees, need to convert to radians for Mathf.Cos and Mathf.Sin
        // cos controls left-right (X), sin controls forward-back (Z), and together they sweep around a circle.
        // (cos θ, sin θ) is always on the unit circle with 1 length or magnitude
        float radians = finalAngleDegrees * Mathf.Deg2Rad;
        Vector3 moveDir = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));

        // Movement
        // Scale movement by current X scale, bigger tornado moves faster
        // moveDir should be normalized already but just in case..
        float scaleFactor = transform.localScale.x;
        transform.position += _currentSpeed * delta * scaleFactor * moveDir.normalized;
    }

    public void SetTornadoStats(TornadoStats tornadoStats, TornadoMovementStats movementStats,
        StatusEffectArgs statusEffectArgs = null, float bonusScale = 0)
    {
        this.movementStats = movementStats;
        this.tornadoStats = tornadoStats;
        Initialize(tornadoStats, bonusScale);

        damageArgs.Damage = tornadoStats.Damage;
        damageCollider.SetDamageArgs(damageArgs);
        damageCollider.SetDamageType(IsDamageOnStay: true, damageInterval: tornadoStats.DamageInterval);
        damageCollider.SetStatusEffectArgs(statusEffectArgs);
    }

    private void Initialize(TornadoStats tornadoStats, float bonusScale = 0)
    {
        _isScalingUp = true;
        _angleDegrees = UnityEngine.Random.Range(0f, 360f);
        _timeOffset = UnityEngine.Random.Range(0f, 999f);
        transform.localScale = Vector3.one * 0.5f; // default scale
        _startUpScale = transform.localScale;
        _targetScaleUp = Vector3.one * (tornadoStats.Scale + bonusScale);
        _targetScaleDown = Vector3.zero;
        _speedTimer = tornadoStats.AttackInterval / 2; // first shot is half the normal interval
    }

    private void AnimateWobbleValues()
    {
        if (!_isWobblingDown && !_isWobblingUp) return;

        _wobbleTime += Time.deltaTime;
        if (_isWobblingDown && _wobbleTime >= movementStats.scaleDownTime)
        {
            _isWobblingDown = false;
        }
        if (_isWobblingUp && _wobbleTime >= movementStats.scaleUpTime)
        {
            _isWobblingUp = false;
        }
    }

    public void OnSpawn() { }

    public void OnDespawn()
    {
        _scalingDownTime = 0;
        _scalingUpTime = 0;
        _elapsedTime = 0;
        _isScalingUp = false;
        _isScalingDown = false;
    }

    public void SetTornadoColor(TornadoColor tornadoColor)
    {
        if (tornadoColor == null) return;

        vfx.SetVector4("Primary Tornado Color", tornadoColor.mainColor);
        vfx.SetVector4("Tornado Base Color", tornadoColor.mainColor);
        vfx.SetVector4("Secondary Tornado Color", tornadoColor.secondaryColor);
    }

    public void SetFinalDamage(float finalDamage, bool isCritical)
    {
        damageArgs.Damage = finalDamage;
        damageArgs.IsCritical = isCritical;
        damageCollider.SetDamageArgs(damageArgs);
    }
}
