using System;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityServiceLocator;
using KodachiGames.Persistence;

namespace KodachiGames.Data.Editor
{
    public class DataBrowserWindow : EditorWindow
    {
        [MenuItem("Kodachi/Data Browser")]
        public static void Open() => GetWindow<DataBrowserWindow>("Data Browser");

        ScrollView _treePane;
        TextField _valuePane;
        Label _statusLabel;

        DataRepository _repo;
        IPersistenceBackend _backend;
        DataContext _context;

        void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            // Toolbar
            var toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    paddingTop = 4, paddingBottom = 4, paddingLeft = 6, paddingRight = 6,
                    borderBottomWidth = 1, borderBottomColor = new Color(0, 0, 0, 0.3f)
                }
            };
            var refreshBtn = new Button(Refresh) { text = "Refresh" };
            toolbar.Add(refreshBtn);

            _statusLabel = new Label { style = { marginLeft = 12, unityTextAlign = TextAnchor.MiddleLeft } };
            toolbar.Add(_statusLabel);
            root.Add(toolbar);

            // Split view
            var split = new TwoPaneSplitView(0, 280, TwoPaneSplitViewOrientation.Horizontal);
            root.Add(split);

            _treePane = new ScrollView { style = { flexGrow = 1, paddingTop = 4, paddingLeft = 6 } };
            split.Add(_treePane);

            var rightContainer = new VisualElement { style = { flexGrow = 1, paddingTop = 4, paddingLeft = 6, paddingRight = 6 } };
            rightContainer.Add(new Label("Value") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } });

            _valuePane = new TextField { multiline = true, isReadOnly = true, style = { flexGrow = 1, whiteSpace = WhiteSpace.Normal } };
            _valuePane.AddToClassList("unity-base-text-field--vertical-scrolling");
            rightContainer.Add(_valuePane);

            split.Add(rightContainer);

            Refresh();
        }

        async void RefreshAsync()
        {
            try
            {
                await RefreshInternalAsync();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void Refresh() => RefreshAsync();

        async Awaitable RefreshInternalAsync()
        {
            _treePane.Clear();
            _valuePane.value = string.Empty;

            if (!Application.isPlaying)
            {
                _statusLabel.text = "Enter Play Mode to browse data.";
                return;
            }

            if (!TryResolveServices())
            {
                _statusLabel.text = "DataRepository not registered. Ensure DataServiceInstaller is in the scene.";
                return;
            }

            _statusLabel.text = string.Empty;

            var profiles = await _repo.GetProfilesAsync();
            if (profiles.ProfileIds.Count == 0)
            {
                _treePane.Add(new Label("(no profiles)") { style = { color = new Color(1, 1, 1, 0.5f) } });
                return;
            }

            var previousProfile = _context.ProfileId;

            foreach (var profileId in profiles.ProfileIds)
            {
                var foldout = new Foldout { text = $"Profile: {profileId}", value = true };
                _context.SetProfile(profileId);

                // Profile Data keys
                var profileDataHeader = new Label("Profile Data") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 4 } };
                foldout.Add(profileDataHeader);

                var profileDataIndex = await LoadProfileDataIndexAsync();
                if (profileDataIndex.Keys.Count == 0)
                    foldout.Add(MutedLabel("  (none)"));
                else
                    foreach (var k in profileDataIndex.Keys)
                    {
                        var fullKey = _context.ProfileDataKey(k);
                        foldout.Add(KeyButton($"  {k}{await VersionSuffixAsync(fullKey)}", fullKey));
                    }

                // Sessions
                var sessionHeader = new Label("Sessions") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 } };
                foldout.Add(sessionHeader);

                var sessionIndex = await _repo.GetSessionsAsync();
                if (sessionIndex.SessionIds.Count == 0)
                    foldout.Add(MutedLabel("  (none)"));
                else
                    foreach (var s in sessionIndex.SessionIds)
                    {
                        var fullKey = _context.SessionDataKey(s);
                        foldout.Add(KeyButton($"  {s}{await VersionSuffixAsync(fullKey)}", fullKey));
                    }

                _treePane.Add(foldout);
            }

            _context.SetProfile(previousProfile);
        }

        bool TryResolveServices()
        {
            _repo = null;
            _backend = null;
            _context = null;

            var anchor = FindFirstObjectByType<DataServiceInstaller>();
            if (anchor == null) return false;

            ServiceLocator.For(anchor).Get<DataRepository>(out _repo);
            ServiceLocator.For(anchor).Get<IPersistenceBackend>(out _backend);
            ServiceLocator.For(anchor).Get<DataContext>(out _context);
            return _repo != null && _backend != null && _context != null;
        }

        async Awaitable<ProfileDataIndex> LoadProfileDataIndexAsync()
        {
            return await _backend.ExistsAsync(_context.ProfileDataIndexKey)
                ? await _backend.LoadAsync<ProfileDataIndex>(_context.ProfileDataIndexKey)
                : new ProfileDataIndex();
        }

        Button KeyButton(string label, string fullKey)
        {
            var btn = new Button(() => ShowValue(fullKey))
            {
                text = label,
                style =
                {
                    unityTextAlign = TextAnchor.MiddleLeft,
                    marginLeft = 0, marginRight = 0, marginTop = 1, marginBottom = 1,
                    paddingLeft = 4
                }
            };
            return btn;
        }

        async void ShowValue(string fullKey)
        {
            try
            {
                if (!await _backend.ExistsAsync(fullKey))
                {
                    _valuePane.value = "(not found)";
                    return;
                }

                var obj = await _backend.LoadAsync<object>(fullKey);
                _valuePane.value = obj?.ToString() ?? "(null)";
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        async Awaitable<string> VersionSuffixAsync(string fullKey)
        {
            var versionKey = fullKey + "/__version";
            if (!await _backend.ExistsAsync(versionKey)) return "  (unversioned)";
            var v = await _backend.LoadAsync<int>(versionKey);
            return $"  (v{v})";
        }

        static Label MutedLabel(string text) =>
            new(text) { style = { color = new Color(1, 1, 1, 0.5f) } };
    }
}
