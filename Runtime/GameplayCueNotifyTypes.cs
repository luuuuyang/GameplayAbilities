using UnityEngine;

namespace GameplayAbilities
{
    public enum GameplayCueNotify_LocallyControlledSource : byte
    {
        InstigatorActor,
        TargetActor,
    }

    public enum GameplayCueNotify_LocallyControlledPolicy : byte
    {
        Always,
        LocalOnly,
        NotLocal,
    }

    public class GameplayCueNotify_SpawnContext
    {
        public GameObject TargetActor;
        public GameplayCueParameters CueParameters;

        public GameplayCueNotify_SpawnCondition DefaultSpawnCondition { set; private get; }
        public GameplayCueNotify_PlacementInfo DefaultPlacementInfo { set; private get; }

        public GameplayCueNotify_PlacementInfo GetPlacementInfo(bool useOverride, in GameplayCueNotify_PlacementInfo placementInfoOverride)
        {
            return !useOverride && DefaultPlacementInfo != null ? DefaultPlacementInfo : placementInfoOverride;
        }

        public GameplayCueNotify_SpawnCondition GetSpawnCondition(bool useOverride, in GameplayCueNotify_SpawnCondition spawnConditionOverride)
        {
            return !useOverride && DefaultSpawnCondition != null ? DefaultSpawnCondition : spawnConditionOverride;
        }

        protected void InitializeContext()
        {

        }
    }

    public class GameplayCueNotify_SpawnResult
    {

    }

    public class GameplayCueNotify_SpawnCondition
    {
        public GameplayCueNotify_LocallyControlledSource LocallyControlledSource;
        public GameplayCueNotify_LocallyControlledPolicy LocallyControlledPolicy;
        public float ChanceToPlay;

        public bool ShouldSpawn(in GameplayCueNotify_SpawnContext spawnContext)
        {
            if (ChanceToPlay < 1f && ChanceToPlay < Random.value)
            {
                return false;
            }

            return true;
        }
    }

    public enum GameplayCueNotify_AttachPolicy : byte
    {
        DoNotAttach,
        AttachToTarget,
    }

    public enum AttachmentRule : byte
    {
        KeepRelative,
        KeepWorld,
        SnapToTarget,
    }

    public class GameplayCueNotify_PlacementInfo
    {
        public string SocketName;
        public GameplayCueNotify_AttachPolicy AttachPolicy;
        public AttachmentRule AttachmentRule;
        public bool OverrideRotation;
        public bool OverrideScale;
        public Quaternion RotationOverride;
        public Vector3 ScaleOverride;

        public bool FindSpawnTransform(in GameplayCueNotify_SpawnContext spawnContext, out Transform spawnTransform)
        {
            spawnTransform = null;

            GameplayCueParameters cueParameters = spawnContext.CueParameters;

            bool setTransform = false;

            if (setTransform)
            {
                if (OverrideRotation)
                {
                    spawnTransform.rotation = RotationOverride;
                }

                if (OverrideScale)
                {
                    spawnTransform.localScale = ScaleOverride;
                }
            }
            else
            {

            }

            return setTransform;
        }
    }

    public class GameplayCueNotify_SoundParameterInterfaceInfo
    {
        public string StopTriggerName = "OnStop";
    }

    public class GameplayCueNotify_SoundInfo
    {
        public GameplayCueNotify_SpawnCondition SpawnConditionOverride;
        public GameplayCueNotify_PlacementInfo PlacementInfoOverride;
        public AudioSource Sound;
        public float LoopingFadeOutDuration;
        public float LoopingFadeVolumeLevel;
        public GameplayCueNotify_SoundParameterInterfaceInfo SoundParameterInterfaceInfo;
        public bool OverrideSpawnCondition;
        public bool OverridePlacementInfo;
        public bool OverrideSoundParameterInterface;

        public bool PlaySound(in GameplayCueNotify_SpawnContext spawnContext, out GameplayCueNotify_SpawnResult result)
        {
            bool soundPlayed = false;

            if (Sound != null)
            {
                GameplayCueNotify_SpawnCondition spawnCondition = spawnContext.GetSpawnCondition(OverrideSpawnCondition, SpawnConditionOverride);
                GameplayCueNotify_PlacementInfo placementInfo = spawnContext.GetPlacementInfo(OverridePlacementInfo, PlacementInfoOverride);

                if (spawnCondition.ShouldSpawn(spawnContext))
                {
                    if (placementInfo.FindSpawnTransform(spawnContext, out Transform spawnTransform))
                    {
                        Sound.Play();
                        soundPlayed = true;
                    }
                }
            }

            result = new GameplayCueNotify_SpawnResult();
            return soundPlayed;
        }
    }
}
