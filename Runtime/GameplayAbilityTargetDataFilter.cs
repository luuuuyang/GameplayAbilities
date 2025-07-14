using System;
using UnityEngine;

namespace GameplayAbilities
{
    public enum TargetDataFilterSelfType
    {
        Any,
        NoSelf,
        NoOthers
    }

    public class GameplayTargetDataFilter
    {
        public GameObject SelfActor;
        public Type RequiredActorClass;
        public TargetDataFilterSelfType SelfFilter = TargetDataFilterSelfType.Any;
        public bool ReverseFilter = false;

        public virtual bool FilterPassesForActor(in GameObject actorToBeFiltered)
        {
            switch (SelfFilter)
            {
                case TargetDataFilterSelfType.NoOthers:
                    if (actorToBeFiltered != SelfActor)
                    {
                        return ReverseFilter ^ false;
                    }
                    break;
                case TargetDataFilterSelfType.NoSelf:
                    if (actorToBeFiltered == SelfActor)
                    {
                        return ReverseFilter ^ false;
                    }
                    break;
                case TargetDataFilterSelfType.Any:
                default:
                    break;
            }

            if (RequiredActorClass != null && !actorToBeFiltered.GetComponent(RequiredActorClass))
            {
                return ReverseFilter ^ false;
            }

            return ReverseFilter ^ true;
        }

        public void InitializeFilterContext(GameObject filterActor)
        {
            SelfActor = filterActor;
        }
    }

    public struct GameplayTargetDataFilterHandle
    {
        public GameplayTargetDataFilter Filter;

        public bool FilterPassesForActor(in GameObject actorToBeFiltered)
        {
            if (actorToBeFiltered == null)
            {
                return Filter == null;
            }

            if (Filter != null)
            {
                if (!Filter.FilterPassesForActor(actorToBeFiltered))
                {
                    return false;
                }
            }

            return true;
        }

        public bool FilterPassesForActor(in WeakReference<GameObject> actorToBeFiltered)
        {
            actorToBeFiltered.TryGetTarget(out GameObject actor);
            return FilterPassesForActor(actor);
        }
    }
}
