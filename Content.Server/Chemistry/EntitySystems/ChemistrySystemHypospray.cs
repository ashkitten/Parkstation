using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Content.Server.Chemistry.Components;
using Content.Server.Chemistry.Components.SolutionManager;
using Content.Server.Weapons.Melee;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Tag;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;
using Content.Shared.Chemistry.Components;

namespace Content.Server.Chemistry.EntitySystems
{
    public sealed partial class ChemistrySystem
    {
        [Dependency] private readonly UseDelaySystem _useDelay = default!;
        [Dependency] private readonly ItemSlotsSystem _itemSlotsSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        private void InitializeHypospray()
        {
            SubscribeLocalEvent<HyposprayComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<HyposprayComponent, MeleeHitEvent>(OnAttack);
            SubscribeLocalEvent<HyposprayComponent, SolutionChangedEvent>(OnSolutionChange);
            SubscribeLocalEvent<HyposprayComponent, EntInsertedIntoContainerMessage>(OnContainerModified);
            SubscribeLocalEvent<HyposprayComponent, EntRemovedFromContainerMessage>(OnContainerModified);
            SubscribeLocalEvent<HyposprayComponent, UseInHandEvent>(OnUseInHand);
            SubscribeLocalEvent<HyposprayComponent, ComponentStartup>(OnStartup);
            SubscribeLocalEvent<HyposprayComponent, ExaminedEvent>(OnExamine);
        }

        private void OnStartup(EntityUid uid, HyposprayComponent component, ComponentStartup args)
        {
            // Necessary for the Client-side HyposprayStatusControl to get the appropriate Volume values.
            Dirty(component);
        }

        private void OnUseInHand(EntityUid uid, HyposprayComponent component, UseInHandEvent args)
        {
            if (args.Handled) return;

            TryDoInject(uid, args.User, args.User);
            args.Handled = true;
        }

        private void OnSolutionChange(EntityUid uid, HyposprayComponent component, SolutionChangedEvent args)
        {
            Dirty(component);
        }

        private void OnContainerModified(EntityUid uid, HyposprayComponent component, ContainerModifiedMessage args)
        {
            Dirty(component);
        }

        public void OnAfterInteract(EntityUid uid, HyposprayComponent component, AfterInteractEvent args)
        {
            if (!args.CanReach)
                return;

            var target = args.Target;
            var user = args.User;

            TryDoInject(uid, target, user);
        }

        public void OnAttack(EntityUid uid, HyposprayComponent component, MeleeHitEvent args)
        {
            if (!args.HitEntities.Any())
                return;

            TryDoInject(uid, args.HitEntities.First(), args.User);
        }

        public bool TryDoInject(EntityUid uid, EntityUid? target, EntityUid user, HyposprayComponent? component=null)
        {
            if (!Resolve(uid, ref component))
                return false;

            if (!EligibleEntity(target, _entMan))
                return false;

            if (TryComp(uid, out UseDelayComponent? delayComp))
                if (_useDelay.ActiveDelay(uid, delayComp))
                    return false;

            string? msgFormat = null;

            if (!component.PierceArmor && _entMan.TryGetComponent<TagComponent>(target, out var tag))
            {
                if (tag.Tags.Contains("HardsuitOn"))
                {
                    if (target == null) return false;
                    _popup.PopupEntity("You cant get the needle to go through the thick plating!", target.Value, user, PopupType.MediumCaution);
                    return false;
                }
            }

            if (target == user)
                msgFormat = "hypospray-component-inject-self-message";
            else if (EligibleEntity(user, _entMan) && _interaction.TryRollClumsy(user, component.ClumsyFailChance))
            {
                msgFormat = "hypospray-component-inject-self-clumsy-message";
                target = user;
            }

            EntityUid? container = uid;

            if (component.SolutionSlot != null) {
                container = _itemSlotsSystem.GetItemOrNull(uid, component.SolutionSlot);
            }

            if (container == null) {
                _popup.PopupCursor(Loc.GetString("hypospray-component-no-container-message"), user);
                return false;
            }

            _solutions.TryGetSolution(container, component.SolutionName, out var hypoSpraySolution);

            if (hypoSpraySolution == null || hypoSpraySolution.Volume == 0)
            {
                _popup.PopupCursor(Loc.GetString("hypospray-component-empty-message"), user);
                return true;
            }

            if (!_solutions.TryGetInjectableSolution(target.Value, out var targetSolution))
            {
                _popup.PopupCursor(Loc.GetString("hypospray-cant-inject", ("target", Identity.Entity(target.Value, _entMan))), user);
                return false;
            }

            _popup.PopupCursor(Loc.GetString(msgFormat ?? "hypospray-component-inject-other-message", ("other", target)), user);

            if (target != user)
            {
                _popup.PopupCursor(Loc.GetString("hypospray-component-feel-prick-message"), target.Value);
                var meleeSys = EntitySystem.Get<MeleeWeaponSystem>();
                var angle = Angle.FromWorldVec(_entMan.GetComponent<TransformComponent>(target.Value).WorldPosition - _entMan.GetComponent<TransformComponent>(user).WorldPosition);
                // TODO: This should just be using melee attacks...
                // meleeSys.SendLunge(angle, user);
            }

            _audio.PlayPvs(component.InjectSound, user);

            // Medipens and such use this system and don't have a delay, requiring extra checks
            // BeginDelay function returns if item is already on delay
            if (delayComp is not null)
                _useDelay.BeginDelay(uid, delayComp);

            // Get transfer amount. May be smaller than component.TransferAmount if not enough room
            var realTransferAmount = FixedPoint2.Min(component.TransferAmount, targetSolution.AvailableVolume);

            if (realTransferAmount <= 0)
            {
                _popup.PopupCursor(Loc.GetString("hypospray-component-transfer-already-full-message",("owner", target)), user);
                return true;
            }

            // Move units from attackSolution to targetSolution
            var removedSolution = _solutions.SplitSolution(container.Value, hypoSpraySolution, realTransferAmount);

            if (!targetSolution.CanAddSolution(removedSolution))
                return true;
            _reactiveSystem.DoEntityReaction(target.Value, removedSolution, ReactionMethod.Injection);
            _solutions.TryAddSolution(target.Value, targetSolution, removedSolution);

            Dirty(component);

            //same logtype as syringes...
            _adminLogger.Add(LogType.ForceFeed, $"{_entMan.ToPrettyString(user):user} injected {_entMan.ToPrettyString(target.Value):target} with a solution {SolutionContainerSystem.ToPrettyString(removedSolution):removedSolution} using a {_entMan.ToPrettyString(uid):using}");

            return true;
        }

        static bool EligibleEntity([NotNullWhen(true)] EntityUid? entity, IEntityManager entMan)
        {
            // TODO: Does checking for BodyComponent make sense as a "can be hypospray'd" tag?
            // In SS13 the hypospray ONLY works on mobs, NOT beakers or anything else.

            return entMan.HasComponent<SolutionContainerManagerComponent>(entity)
                && entMan.HasComponent<MobStateComponent>(entity);
        }

        private void OnExamine(EntityUid uid, HyposprayComponent component, ExaminedEvent args)
        {
            if (component.SolutionSlot != null) {
                var container = _itemSlotsSystem.GetItemOrNull(uid, component.SolutionSlot);
                if (container == null) {
                    args.PushText(Loc.GetString("hypospray-component-on-examine-no-container"));
                    return;
                }

                if (!TryComp(container, out ExaminableSolutionComponent? examinableComponent))
                    return;

                // Mostly copied from SolutionContainerSystem.OnExamineSolution

                SolutionContainerManagerComponent? solutionsManager = null;
                if (!Resolve(container.Value, ref solutionsManager)
                    || !solutionsManager.Solutions.TryGetValue(examinableComponent.Solution, out var solutionHolder))
                    return;

                var primaryReagent = solutionHolder.GetPrimaryReagentId();

                if (string.IsNullOrEmpty(primaryReagent))
                {
                    args.PushText(Loc.GetString("shared-solution-container-component-on-examine-empty-container"));
                    return;
                }

                if (!_prototypeManager.TryIndex(primaryReagent, out ReagentPrototype? proto))
                {
                    Logger.Error(
                        $"{nameof(Solution)} could not find the prototype associated with {primaryReagent}.");
                    return;
                }

                var colorHex = solutionHolder.GetColor(_prototypeManager)
                    .ToHexNoAlpha(); //TODO: If the chem has a dark color, the examine text becomes black on a black background, which is unreadable.
                var messageString = "shared-solution-container-component-on-examine-main-text";

                args.PushMarkup(Loc.GetString(messageString,
                    ("color", colorHex),
                    ("wordedAmount", Loc.GetString(solutionHolder.Contents.Count == 1
                        ? "shared-solution-container-component-on-examine-worded-amount-one-reagent"
                        : "shared-solution-container-component-on-examine-worded-amount-multiple-reagents")),
                    ("desc", proto.LocalizedPhysicalDescription)));
            }
        }
    }
}
