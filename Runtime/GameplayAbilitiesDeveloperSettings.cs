using GameplayTags;
using UnityEditor;
using UnityEngine;

namespace GameplayAbilities
{
    public class GameplayAbilitiesDeveloperSettings : ScriptableSingleton<GameplayAbilitiesDeveloperSettings>
    {

        public GameplayTag ActivateFailCanActivateAbilityTag;

        /** TryActivate failed due to being on cooldown */


        public GameplayTag ActivateFailCooldownTag;


        /** TryActivate failed due to not being able to spend costs */

        public GameplayTag ActivateFailCostTag;


        /** Failed to activate due to invalid networking settings, this is designer error */

        public GameplayTag ActivateFailNetworkingTag;


        /** TryActivate failed due to being blocked by other abilities */

        public GameplayTag ActivateFailTagsBlockedTag;


        /** TryActivate failed due to missing required tags */

        public GameplayTag ActivateFailTagsMissingTag;

    }
}
