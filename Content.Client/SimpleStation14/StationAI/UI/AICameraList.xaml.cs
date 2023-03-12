using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.XAML;
using Content.Shared.SimpleStation14.StationAI;
using Robust.Client.UserInterface.Controls;
using Content.Client.UserInterface.Controls;

namespace Content.Client.SimpleStation14.StationAI.UI
{
    [GenerateTypedNameReferences]
    public sealed partial class AICameraList : FancyWindow
    {
        private AICameraComponent? _selectedCamera;
        private List<EntityUid> _cameras = new();
        public event Action? TryUpdateCameraList;

        public AICameraList()
        {
            RobustXamlLoader.Load(this);

            SubnetList.OnItemSelected += ItemSelected;
            SubnetList.OnItemDeselected += ItemDeselected;
            SearchBar.OnTextChanged += (_) => FillCameraList(SearchBar.Text);
            Refresh.OnPressed += (_) => TryUpdateCameraList?.Invoke();
        }

        private void ItemSelected(ItemList.ItemListSelectedEventArgs obj)
        {
            _selectedCamera = (AICameraComponent) obj.ItemList[obj.ItemIndex].Metadata!;
            FillCameraList();
        }

        private void ItemDeselected(ItemList.ItemListDeselectedEventArgs obj)
        {
            _selectedCamera = null;
            FillCameraList();
        }

        public void FillCameraList(string? filter = null)
        {
            SubnetList.Clear();

            if (_cameras.Count == 0)
            {
                Text.Text = "No cameras found.";
                return;
            }

            Text.Text = "Select a camera to view it.";

            foreach (var uid in _cameras)
            {
                if (!IoCManager.Resolve<IEntityManager>().TryGetComponent<AICameraComponent>(uid, out var camera)) continue;

                if (camera.Enabled == false) continue;

                if (!string.IsNullOrEmpty(filter) && !camera.CameraName.ToLowerInvariant().Contains(filter.Trim().ToLowerInvariant()))
                    continue;

                ItemList.Item cameraItem = new(SubnetList)
                {
                    Metadata = camera,
                    Text = camera.CameraName
                };

                SubnetList.Add(cameraItem);
            }
        }

        public void UpdateCameraList(List<EntityUid>? cameras = null)
        {
            if (cameras == null)
            {
                TryUpdateCameraList?.Invoke();
                return;
            }

            _cameras = cameras;
            FillCameraList();
        }
    }
}
