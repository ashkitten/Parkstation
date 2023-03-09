using Content.Shared.Actions;
using Content.Shared.Actions.ActionTypes;
using Content.Shared.Bed.Sleep;
using Content.Shared.SimpleStation14.Species.Shadekin.Components;
using Content.Shared.SimpleStation14.Species.Shadekin.Events;
using Content.Shared.StatusEffect;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.SimpleStation14.Species.Shadekin.Systems
{
    public sealed class ShadekinRestSystem : EntitySystem
    {
        [Dependency] private readonly ShadekinPowerSystem _powerSystem = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly StatusEffectsSystem _statusEffectSystem = default!;
        [Dependency] private readonly INetManager _net = default!;

        private InstantAction action = default!;

        public override void Initialize()
        {
            base.Initialize();

            action = new InstantAction(_prototypeManager.Index<InstantActionPrototype>("ShadekinRest"));

            SubscribeLocalEvent<ShadekinRestEventResponse>(Rest);

            SubscribeLocalEvent<ShadekinRestPowerComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<ShadekinRestPowerComponent, ComponentShutdown>(OnShutdown);
        }

        private void Rest(ShadekinRestEventResponse args)
        {
            if (!_entityManager.TryGetComponent<ShadekinComponent>(args.Performer, out var shadekin)) return;
            if (!_entityManager.TryGetComponent<ShadekinRestPowerComponent>(args.Performer, out var rest)) return;
            rest.IsResting = args.IsResting;

            if (args.IsResting)
            {
                _statusEffectSystem.TryAddStatusEffect<ForcedSleepingComponent>(args.Performer, "ForcedSleep", TimeSpan.FromDays(1), false);

                _powerSystem.TryAddMultiplier(args.Performer, 1f);
            }
            else
            {
                _statusEffectSystem.TryRemoveStatusEffect(args.Performer, "ForcedSleep");

                _powerSystem.TryAddMultiplier(args.Performer, -1f);
            }
        }


        private void OnStartup(EntityUid uid, ShadekinRestPowerComponent component, ComponentStartup args)
        {
            _actionsSystem.AddAction(uid, action, uid);
        }

        private void OnShutdown(EntityUid uid, ShadekinRestPowerComponent component, ComponentShutdown args)
        {
            _actionsSystem.RemoveAction(uid, action);
        }
    }
}
