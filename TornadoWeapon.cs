using Lean.Pool;
using MyBox;
using UnityEngine;

public class TornadoWeapon : BoostableDamageBehavior, IWeaponInterface
{
    [SerializeField] private GameObject tornadoPrefab;

    [Header("Tornado Stats")]
    [SerializeField] private TornadoStats tornadoStats;
    [SerializeField] private TornadoMovementStats movementStats;
    [SerializeField] private TornadoColor tornadoColor;

    [Header("Status Effect")]
    [SerializeField] private StatusEffectArgs statusEffectArgs;

    [Header("Augments")]
    [SerializeField] private SizeAugment sizeAugment;
    [SerializeField] private SpreadAugment spreadAugment;
    [SerializeField] private AttackSpeedAugment attackSpeedAugment;

    [Header("Upgrade Settings SO")]
    [SerializeField][DisplayInspector] private TornadoUpgradeSettings tornadoUpgradeSettings;

    private int _currentLevel;
    private float _attackTimer;
    private float _baseWeaponDmgMultiplier = 1;

    private float CurrentAttackInterval => tornadoStats.AttackRate - attackSpeedAugment.shootIntervalDeduction;
    private float TotalProjectileCount => tornadoStats.Count + spreadAugment.bonusProjectileCount;

    new void Start()
    {
        base.Start();
        if (GameEvents.Instance) GameEvents.Instance.OnPlayerUpgraded += OnPlayerUpgraded;
    }

    void OnDestroy()
    {
        if (GameEvents.Instance) GameEvents.Instance.OnPlayerUpgraded -= OnPlayerUpgraded;
    }

    void Update()
    {
        if (_gameState == null || _gameState.IsPaused) return;
        HandleAttackTimer();
    }

    private void HandleAttackTimer()
    {
        _attackTimer += Time.deltaTime;
        if (_attackTimer >= CurrentAttackInterval)
        {
            _attackTimer = 0f;
            for (int i = 0; i < TotalProjectileCount; i++)
            {
                SpawnTornado();
            }
        }
    }

    [ButtonMethod]
    public void SpawnTornado()
    {
        GameObject tornadoInstance = LeanPool.Spawn(tornadoPrefab, transform.position, Quaternion.identity);
        if (tornadoInstance == null)
        {
            Debug.LogError("TornadoWeapon - SpawnTornado() - Failed to spawn TornadoPrefab.");
            return;
        }

        bool isCritical = IsCriticalDamage();
        float finalDamage = GetFinalTornadoDamage(isCritical);

        if (tornadoInstance.TryGetComponent(out TornadoProjectile tornadoProjectile))
        {
            tornadoProjectile.SetFinalDamage(finalDamage, isCritical);
            tornadoProjectile.SetTornadoStats(tornadoStats, movementStats, statusEffectArgs, sizeAugment.bonusSize);
            tornadoProjectile.SetTornadoColor(tornadoColor);
        }
    }

    private float GetFinalTornadoDamage(bool isCritical)
    {
        float finalDamage = GetFinalDamage(tornadoStats.Damage * _baseWeaponDmgMultiplier, isCritical);
        if (spreadAugment.bonusProjectileCount > 0)
        {
            finalDamage *= spreadAugment.spreadDamageMultiplier;
        }
        return finalDamage;
    }

    public void SetLevelStats(int level)
    {
        ApplyBasicStats(level);
        ApplyStatusEffectStats(level);
    }

    private void ApplyStatusEffectStats(int level)
    {
        TryGetUpgradeSettingsSO();
        if (tornadoUpgradeSettings == null || tornadoUpgradeSettings.statusEffectArgsPerLevel.Length == 0)
        {
            Logger.LogWarning($"TornadoWeapon - ApplyStatusEffectStats() - TornadoUpgradeSettings or statusEffectArgsPerLevel is not assigned.");
            return;
        }

        int statusIndex = Mathf.Clamp(level, 0, tornadoUpgradeSettings.statusEffectArgsPerLevel.Length - 1);
        statusEffectArgs = tornadoUpgradeSettings.statusEffectArgsPerLevel[statusIndex];
        tornadoColor = tornadoUpgradeSettings.statusEffectColors[statusEffectArgs.StatusEffect];
    }

    private void ApplyBasicStats(int level)
    {
        TryGetUpgradeSettingsSO();
        if (tornadoUpgradeSettings == null || tornadoUpgradeSettings.perLevelSettings.Length == 0)
        {
            Logger.LogWarning($"TornadoWeapon - ApplyStatusEffectStats() - TornadoUpgradeSettings or perLevelSettings is not assigned.");
            return;
        }

        int index = Mathf.Clamp(level, 0, tornadoUpgradeSettings.perLevelSettings.Length - 1);
        tornadoStats = tornadoUpgradeSettings.perLevelSettings[index];
        movementStats = tornadoUpgradeSettings.movementStats;
    }

    #region IWeaponInterface

    public void SetBaseLevel()
    {
        _currentLevel = 0;
        SetLevelStats(_currentLevel);
    }

    public void IncreaseLevel()
    {
        _currentLevel++;
        SetLevelStats(_currentLevel);
    }

    public void SetBaseWeaponDamageMultiplier(float damageMultiplier = 1)
    {
        _baseWeaponDmgMultiplier = damageMultiplier;
    }

    public void OnPlayerUpgraded(UpgradeType type, bool arg2, bool arg3)
    {
        TryGetUpgradeSettingsSO();

        if (tornadoUpgradeSettings == null)
        {
            Logger.LogWarning($"TornadoWeapon - OnPlayerUpgraded() - TornadoUpgradeSettings is not assigned.");
            return;
        }

        if (!tornadoUpgradeSettings.acceptsAugments)
        {
            Logger.LogWarning($"TornadoWeapon - OnPlayerUpgraded() - TornadoUpgradeSettings does not accept augments.");
            return;
        }

        int upgradeLevel = PlayerUpgradesManager.Instance.GetCurrentUpgradeLevel(type);

        if (type == UpgradeType.AttackSpeed && tornadoUpgradeSettings.HasAugmentSupport(AugmentType.AttackSpeed))
        {
            attackSpeedAugment = GetAugment(tornadoUpgradeSettings.attackSpeedAugmentSettings, upgradeLevel);
        }
        else if (type == UpgradeType.ProjectileSize && tornadoUpgradeSettings.HasAugmentSupport(AugmentType.Size))
        {
            sizeAugment = GetAugment(tornadoUpgradeSettings.sizeAugmentSettings, upgradeLevel);
        }
        else if (type == UpgradeType.Spread && tornadoUpgradeSettings.HasAugmentSupport(AugmentType.Spread))
        {
            spreadAugment = GetAugment(tornadoUpgradeSettings.spreadAugmentSettings, upgradeLevel);
        }
        else
        {
            Logger.LogWarning($"TornadoWeapon - OnPlayerUpgraded() - UpgradeType {type} is not supported by TornadoWeapon.");
        }
    }

    public void TryGetUpgradeSettingsSO()
    {
        if (tornadoUpgradeSettings == null)
        {
            tornadoUpgradeSettings = InLevelUpgradesDataSO.Instance
                .GetUpgradeData<TornadoUpgradeSettings>(UpgradeType.Tornado);
        }
    }
    #endregion IWeaponInterface

    private T GetAugment<T>(T[] settings, int upgradeLevel)
        => settings[Mathf.Clamp(upgradeLevel - 1, 0, settings.Length - 1)];
}
