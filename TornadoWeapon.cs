using Lean.Pool;
using MyBox;
using UnityEngine;

public class TornadoWeapon : BoostableDamageBehavior, IWeaponInterface
{
    [SerializeField] private GameObject tornadoPrefab;

    [SerializeField][ReadOnly] private TornadoStats tornadoStats;
    [SerializeField][ReadOnly] private TornadoMovementStats movementStats;

    [Header("Status Effect")]
    [SerializeField] private StatusEffectArgs statusEffectArgs;

    [Header("Augments")]
    [SerializeField] private SizeAugment sizeAugment;
    [SerializeField] private SpreadAugment spreadAugment;
    [SerializeField] private AttackSpeedAugment attackSpeedAugment;

    [Header("Upgrade Settings")]
    [SerializeField][DisplayInspector] private TornadoUpgradeSettings tornadoUpgradeSettings;

    private float attackTimer;
    private int _currentLevel;
    private float _baseWeaponDmgMultiplier;

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
        if (gameState == null || gameState.IsPaused) return;
        
        HandleAttackTimer();
    }

    private void HandleAttackTimer()
    {
        attackTimer += Time.deltaTime;
        if (attackTimer >= tornadoStats.AttackRate - attackSpeedAugment.shootIntervalDeduction)
        {
            attackTimer = 0f;
            for (int i = 0; i < tornadoStats.Count + spreadAugment.bonusProjectileCount; i++)
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
            Debug.LogError("Failed to spawn TornadoPrefab.");
            return;
        }

        bool isCritical = IsCriticalDamage();
        float finalDamage = GetFinalDamage(tornadoStats.Damage * _baseWeaponDmgMultiplier, isCritical);
        if (spreadAugment.bonusProjectileCount > 0)
        {
            finalDamage *= spreadAugment.spreadDamageMultiplier;
        }

        if (tornadoInstance.TryGetComponent(out TornadoProjectile tornadoProjectile))
        {
            int index = Mathf.Clamp(_currentLevel, 0, tornadoUpgradeSettings.statusEffectArgsPerLevel.Length - 1);
            statusEffectArgs = tornadoUpgradeSettings.statusEffectArgsPerLevel[index];
            tornadoProjectile.SetFinalDamage(finalDamage, isCritical);
            tornadoProjectile.SetTornadoStats(tornadoStats, movementStats, statusEffectArgs, sizeAugment.bonusSize);
            tornadoProjectile.SetTornadoColor(tornadoUpgradeSettings.statusEffectColors[statusEffectArgs.StatusEffect]);
        }
    }

    public void SetBaseLevel()
    {
        TryGetUpgradeSettingsSO();
        _currentLevel = 0;
        SetLevelStats(_currentLevel);
    }

    public void IncreaseLevel()
    {
        TryGetUpgradeSettingsSO();
        _currentLevel++;
        SetLevelStats(_currentLevel);
    }

    [ButtonMethod]
    public void SetCurrentLevelStats()
    {
        SetLevelStats(_currentLevel);
    }

    public void SetLevelStats(int level)
    {
        int index = Mathf.Clamp(level, 0, tornadoUpgradeSettings.perLevelSettings.Length - 1);
        tornadoStats = tornadoUpgradeSettings.perLevelSettings[index];
        movementStats = tornadoUpgradeSettings.movementStats;
    }

    public void SetBaseWeaponDamageMultiplier(float damageMultiplier = 1)
    {
        _baseWeaponDmgMultiplier = damageMultiplier;
    }

    public void OnPlayerUpgraded(UpgradeType type, bool arg2, bool arg3)
    {
        TryGetUpgradeSettingsSO();

        if (tornadoUpgradeSettings == null) return;

        if (!tornadoUpgradeSettings.acceptsAugments) return;

        int upgradeLevel = PlayerUpgradesManager.Instance.GetCurrentUpgradeLevel(type);

        if (type == UpgradeType.AttackSpeed && tornadoUpgradeSettings.HasAugmentSupport(AugmentType.AttackSpeed))
        {
            IncreaseAttackSpeedLevel(upgradeLevel);
        }
        else if (type == UpgradeType.ProjectileSize && tornadoUpgradeSettings.HasAugmentSupport(AugmentType.Size))
        {
            IncreaseProjectileSizeLevel(upgradeLevel);
        }
        else if (type == UpgradeType.Spread && tornadoUpgradeSettings.HasAugmentSupport(AugmentType.Spread))
        {
            IncreaseSpreadLevel(upgradeLevel);
        }
    }

    private void IncreaseSpreadLevel(int upgradeLevel)
    {
        TryGetUpgradeSettingsSO();
        int index = Mathf.Clamp(upgradeLevel - 1, 0, tornadoUpgradeSettings.spreadAugmentSettings.Length - 1);
        spreadAugment = tornadoUpgradeSettings.spreadAugmentSettings[index];
    }

    private void IncreaseProjectileSizeLevel(int upgradeLevel)
    {
        TryGetUpgradeSettingsSO();
        int index = Mathf.Clamp(upgradeLevel - 1, 0, tornadoUpgradeSettings.sizeAugmentSettings.Length - 1);
        sizeAugment = tornadoUpgradeSettings.sizeAugmentSettings[index];
    }

    private void IncreaseAttackSpeedLevel(int upgradeLevel)
    {
        TryGetUpgradeSettingsSO();
        int index = Mathf.Clamp(upgradeLevel - 1, 0, tornadoUpgradeSettings.attackSpeedAugmentSettings.Length - 1);
        attackSpeedAugment = tornadoUpgradeSettings.attackSpeedAugmentSettings[index];
    }

    public void TryGetUpgradeSettingsSO()
    {
        if (tornadoUpgradeSettings == null)
        {
            tornadoUpgradeSettings = InLevelUpgradesDataSO.Instance
                .GetUpgradeData<TornadoUpgradeSettings>(UpgradeType.Tornado);
        }
    }
}
