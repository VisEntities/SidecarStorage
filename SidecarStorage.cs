using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Sidecar Storage", "VisEntities", "1.0.0")]
    [Description("Adds stash containers to motorbike sidecars.")]
    public class SidecarStorage : RustPlugin
    {
        #region Fields

        private static SidecarStorage _plugin;
        private static Configuration _config;

        private const string PREFAB_STASH = "assets/prefabs/deployable/small stash/small_stash_deployed.prefab";
        private const string PREFAB_SIDECAR = "assets/content/vehicles/bikes/motorbike_sidecar.prefab";
        
        private static readonly Vector3 _stashPosition = new Vector3(0.72f, 0.52f, -0.69f);
        private static readonly Vector3 _stashRotation = new Vector3(0f, 90f, -85f);

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Stash Container Inventory Size")]
            public int StashContainerInventorySize { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                StashContainerInventorySize = 6
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
        }

        private void Unload()
        {
            RemoveOrAddSidecarStashContainers(isUnloading: true);
            _config = null;
            _plugin = null;
        }

        private void OnServerInitialized(bool isStartup)
        {
            RemoveOrAddSidecarStashContainers(isUnloading: false);
        }

        private void OnEntitySpawned(Bike bike)
        {
            if (bike != null && bike.PrefabName == PREFAB_SIDECAR)
            {
                SpawnStashContainer(bike);
            }
        }

        #endregion Oxide Hooks

        #region Stash Container Spawn And Removal

        private StashContainer SpawnStashContainer(Bike bike)
        {
            StashContainer stashContainer = GameManager.server.CreateEntity(PREFAB_STASH, _stashPosition, Quaternion.Euler(_stashRotation)) as StashContainer;
            if (stashContainer == null)
                return null;

            stashContainer.SetParent(bike);
            stashContainer.Spawn();
            stashContainer.inventory.capacity = _config.StashContainerInventorySize;

            RemoveProblematicComponents(stashContainer);
            return stashContainer;
        }
        
        private void RemoveOrAddSidecarStashContainers(bool isUnloading)
        {
            foreach (Bike bike in BaseNetworkable.serverEntities.OfType<Bike>())
            {
                if (bike != null && bike.PrefabName == PREFAB_SIDECAR)
                {
                    List<StashContainer> attachedStashes = FindChildrenOfType<StashContainer>(bike, PREFAB_STASH);

                    if (isUnloading)
                    {
                        foreach (StashContainer stash in attachedStashes)
                        {
                            if (stash != null)
                                stash.Kill();
                        }
                    }
                    else
                    {
                        if (attachedStashes.Count == 0)
                        {
                            SpawnStashContainer(bike);
                        }
                    }
                }
            }
        }

        #endregion Stash Container Spawn And Removal

        #region Helper Functions

        private static void RemoveProblematicComponents(BaseEntity entity)
        {
            foreach (var collider in entity.GetComponentsInChildren<Collider>())
            {
                if (!collider.isTrigger)
                    UnityEngine.Object.DestroyImmediate(collider);
            }

            UnityEngine.Object.DestroyImmediate(entity.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<GroundWatch>());
        }

        private static List<T> FindChildrenOfType<T>(BaseEntity parentEntity, string prefabName = null) where T : BaseEntity
        {
            List<T> foundChildren = new List<T>();
            foreach (BaseEntity child in parentEntity.children)
            {
                T childOfType = child as T;
                if (childOfType != null && (prefabName == null || child.PrefabName == prefabName))
                    foundChildren.Add(childOfType);
            }

            return foundChildren;
        }

        #endregion Helper Functions
    }
}