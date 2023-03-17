using Content.Shared.Actions;
using Content.Shared.Actions.ActionTypes;
using Content.Shared.StatusEffect;
using Content.Shared.Abilities.Psionics;
using Content.Shared.SimpleStation14.StationAI;
using Content.Server.Mind.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Server.Abilities.Psionics;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.SimpleStation14.StationAI
{
    public sealed class AITelegnosisPowerSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
        [Dependency] private readonly SharedActionsSystem _actions = default!;
        [Dependency] private readonly MindSwapPowerSystem _mindSwap = default!;
        [Dependency] private readonly SharedPsionicAbilitiesSystem _psionics = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<AITelegnosisPowerComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<AITelegnosisPowerComponent, ComponentShutdown>(OnShutdown);
            SubscribeLocalEvent<AITelegnosticProjectionComponent, MindRemovedMessage>(OnMindRemoved);

            SubscribeLocalEvent<AITelegnosisPowerComponent, AITelegnosisPowerActionEvent>(OnPowerUsed);

            SubscribeLocalEvent<StationAIComponent, MobStateChangedEvent>(OnMobStateChanged);
        }

        private void OnInit(EntityUid uid, AITelegnosisPowerComponent component, ComponentInit args)
        {
            if (!_prototypeManager.TryIndex<InstantActionPrototype>("AIeye", out var mindswap))
                return;

            if (_entityManager.TryGetComponent<AITelegnosticProjectionComponent>(uid, out var _))
                return;

            component.TelegnosisPowerAction = new InstantAction(mindswap);
            _actions.AddAction(uid, component.TelegnosisPowerAction, null);

            if (TryComp<PsionicComponent>(uid, out var psionic) && psionic.PsionicAbility == null)
                psionic.PsionicAbility = component.TelegnosisPowerAction;
        }

        private void OnShutdown(EntityUid uid, AITelegnosisPowerComponent component, ComponentShutdown args)
        {
            if (_entityManager.TryGetComponent<AITelegnosticProjectionComponent>(uid, out var _))
                return;

            if (_prototypeManager.TryIndex<InstantActionPrototype>("AIeye", out var mindswap))
                _actions.RemoveAction(uid, new InstantAction(mindswap), null);
        }

        private void OnPowerUsed(EntityUid uid, AITelegnosisPowerComponent component, AITelegnosisPowerActionEvent args)
        {
            var ai = _entityManager.EnsureComponent<StationAIComponent>(uid);

            var projection = Spawn(component.Prototype, Transform(uid).Coordinates);
            ai.ActiveEye = projection;
            var core = _entityManager.GetComponent<MetaDataComponent>(uid);

            if (core.EntityName != "") _entityManager.GetComponent<MetaDataComponent>(projection).EntityName = core.EntityName;
            else _entityManager.GetComponent<MetaDataComponent>(projection).EntityName = "Invalid AI";

            Transform(projection).AttachToGridOrMap();
            _mindSwap.Swap(uid, projection);


            _psionics.LogPowerUsed(uid, "aieye");

            args.Handled = true;
        }

        private void OnMindRemoved(EntityUid uid, AITelegnosticProjectionComponent component, MindRemovedMessage args)
        {
            QueueDel(uid);
        }

        private void OnMobStateChanged(EntityUid uid, StationAIComponent component, MobStateChangedEvent args)
        {
            if (!_mobState.IsDead(uid)) return;

            if (component.ActiveEye != EntityUid.Invalid) _mindSwap.Swap(component.ActiveEye, uid);
            else Logger.Error("AI died without an active eye");
            SoundSystem.Play("/Audio/SimpleStation14/Machines/AI/borg_death.ogg", Filter.Pvs(uid), uid);
        }
    }

    public sealed class AITelegnosisPowerActionEvent : InstantActionEvent { }
}
