using KodachiGames.Markdown.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityServiceLocator;
using KodachiGames.Persistence;

namespace KodachiGames.Data.Editor
{
    public class DataBrowserWindow : EditorWindow
    {
        private const string LiveSource = "Live (Play Mode)";
        private const int HexPreviewBytes = 256;

        [MenuItem("Kodachi/Data Browser")]
        public static void Open() => GetWindow<DataBrowserWindow>("Data Browser");

        ScrollView _treePane;
        VisualElement _valuePane;
        Label _statusLabel;
        DropdownField _sourceField;
        DropdownField _formatField;

        DataRepository _repo;
        IPersistenceBackend _backend;
        DataContext _context;

        List<Type> _backendTypes;
        List<Type> _formatTypes;
        List<IDataBrowserPreview> _previews;

        void CreateGUI()
        {
            _backendTypes = ConcreteTypes<IPersistenceBackend>()
                .Where(t => !typeof(IHydratedBackend).IsAssignableFrom(t))
                .ToList();
            _formatTypes = ConcreteTypes<ISaveFormat>().ToList();
            _previews = ConcreteTypes<IDataBrowserPreview>()
                .Select(t => (IDataBrowserPreview)Activator.CreateInstance(t))
                .ToList();

            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            var toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 4, paddingBottom = 4, paddingLeft = 6, paddingRight = 6,
                    borderBottomWidth = 1, borderBottomColor = new Color(0, 0, 0, 0.3f)
                }
            };
            toolbar.Add(new Button(Refresh) { text = "Refresh" });

            var sources = new List<string> { LiveSource };
            sources.AddRange(_backendTypes.Select(DisplayName));
            _sourceField = new DropdownField("Source", sources, 0) { style = { minWidth = 260, marginLeft = 8 } };
            _sourceField.RegisterValueChangedCallback(_ => Refresh());
            toolbar.Add(_sourceField);

            int jsonIndex = _formatTypes.IndexOf(typeof(JsonSaveFormat));
            _formatField = new DropdownField("Format", _formatTypes.Select(DisplayName).ToList(), jsonIndex)
                { style = { minWidth = 220, marginLeft = 8 } };
            _formatField.RegisterValueChangedCallback(_ => Refresh());
            toolbar.Add(_formatField);

            _statusLabel = new Label { style = { marginLeft = 12, unityTextAlign = TextAnchor.MiddleLeft } };
            toolbar.Add(_statusLabel);
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });
            toolbar.Add(WindowGuide.Button(typeof(DataBrowserWindow), "data-browser"));
            root.Add(toolbar);

            var split = new TwoPaneSplitView(0, 280, TwoPaneSplitViewOrientation.Horizontal);
            root.Add(split);

            _treePane = new ScrollView { style = { flexGrow = 1, paddingTop = 4, paddingLeft = 6 } };
            split.Add(_treePane);

            var rightContainer = new VisualElement { style = { flexGrow = 1, paddingTop = 4, paddingLeft = 6, paddingRight = 6 } };
            rightContainer.Add(new Label("Value") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } });
            _valuePane = new VisualElement { style = { flexGrow = 1 } };
            rightContainer.Add(_valuePane);
            split.Add(rightContainer);

            Refresh();
        }

        static IEnumerable<Type> ConcreteTypes<T>() =>
            TypeCache.GetTypesDerivedFrom<T>()
                .Where(t => !t.IsAbstract && !t.IsInterface && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(DisplayName);

        static string DisplayName(Type type) => SelectorName.GetDisplayName(type);

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
            _valuePane.Clear();

            if (!TryResolveSource(out string problem))
            {
                _statusLabel.text = problem;
                return;
            }

            _statusLabel.text = string.Empty;

            var profiles = await _repo.GetProfilesAsync();
            if (profiles.ProfileIds.Count == 0)
            {
                _treePane.Add(MutedLabel("(no profiles)"));
                return;
            }

            var previousProfile = _context.ProfileId;

            foreach (var profileId in profiles.ProfileIds)
            {
                var foldout = new Foldout { text = $"Profile: {profileId}", value = true };
                _context.SetProfile(profileId);

                foldout.Add(new Label("Profile Data") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 4 } });

                var profileDataIndex = await _repo.GetProfileDataIndexAsync();
                if (profileDataIndex.Keys.Count == 0)
                    foldout.Add(MutedLabel("  (none)"));
                else
                    foreach (var k in profileDataIndex.Keys)
                    {
                        var fullKey = _context.ProfileDataKey(k);
                        var owner = profileId;
                        foldout.Add(KeyButton($"  {k}{await VersionSuffixAsync(fullKey)}", fullKey,
                            () => DeleteInProfileAsync(owner, () => _repo.DeleteProfileDataAsync(k))));
                    }

                foldout.Add(new Label("Sessions") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 6 } });

                var sessionIndex = await _repo.GetSessionsAsync();
                if (sessionIndex.SessionIds.Count == 0)
                    foldout.Add(MutedLabel("  (none)"));
                else
                    foreach (var s in sessionIndex.SessionIds)
                    {
                        var fullKey = _context.SessionDataKey(s);
                        var owner = profileId;
                        foldout.Add(KeyButton($"  {s}{await VersionSuffixAsync(fullKey)}", fullKey,
                            () => DeleteInProfileAsync(owner, () => _repo.DeleteSessionAsync(s))));
                    }

                _treePane.Add(foldout);
            }

            _context.SetProfile(previousProfile);
        }

        bool TryResolveSource(out string problem)
        {
            _repo = null;
            _backend = null;
            _context = null;

            if (_sourceField.index == 0)
            {
                if (!Application.isPlaying)
                {
                    problem = "Enter Play Mode, or pick a backend under Source to read it directly.";
                    return false;
                }

                var anchor = FindFirstObjectByType<DataServiceInstaller>();
                if (anchor == null)
                {
                    problem = "DataRepository not registered. Ensure DataServiceInstaller is in the scene.";
                    return false;
                }

                ServiceLocator.For(anchor).Get(out _repo);
                ServiceLocator.For(anchor).Get(out _backend);
                ServiceLocator.For(anchor).Get(out _context);
                problem = null;
                return true;
            }

            _backend = (IPersistenceBackend)Activator.CreateInstance(_backendTypes[_sourceField.index - 1]);
            _context = new DataContext();
            var format = (ISaveFormat)Activator.CreateInstance(_formatTypes[_formatField.index]);
            _repo = new DataRepository(_backend, format, _context);
            problem = null;
            return true;
        }

        async Awaitable DeleteInProfileAsync(string profileId, Func<Awaitable> delete)
        {
            var previous = _context.ProfileId;
            _context.SetProfile(profileId);
            try
            {
                await delete();
            }
            finally
            {
                _context.SetProfile(previous);
            }
        }

        Button KeyButton(string label, string fullKey, Func<Awaitable> delete) =>
            new(() => ShowValue(fullKey, delete))
            {
                text = label,
                style =
                {
                    unityTextAlign = TextAnchor.MiddleLeft,
                    marginLeft = 0, marginRight = 0, marginTop = 1, marginBottom = 1,
                    paddingLeft = 4
                }
            };

        async void ShowValue(string fullKey, Func<Awaitable> delete)
        {
            try
            {
                _valuePane.Clear();
                var actions = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
                actions.Add(new Label(fullKey) { style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft } });
                actions.Add(new Button(() => ConfirmDelete(fullKey, delete)) { text = "Delete" });
                _valuePane.Add(actions);

                if (!await _backend.ExistsAsync(fullKey))
                {
                    _valuePane.Add(MutedLabel("(not found)"));
                    return;
                }

                byte[] bytes = await _backend.ReadAsync(fullKey);
                IDataBrowserPreview preview = _previews.FirstOrDefault(p => p.CanPreview(bytes));
                _valuePane.Add(preview != null ? preview.CreatePreview(fullKey, bytes) : TextView(Describe(bytes)));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        async void ConfirmDelete(string fullKey, Func<Awaitable> delete)
        {
            try
            {
                if (!EditorUtility.DisplayDialog("Delete saved data",
                        $"Delete '{fullKey}' from {_sourceField.value}?\n\nThis cannot be undone.", "Delete", "Cancel"))
                    return;

                await delete();
                Debug.Log($"[Data Browser] Deleted '{fullKey}' from {_sourceField.value}.");
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

        async Awaitable<string> VersionSuffixAsync(string fullKey)
        {
            var versionKey = fullKey + "/__version";
            if (!await _backend.ExistsAsync(versionKey)) return "  (unversioned)";
            return $"  ({Describe(await _backend.ReadAsync(versionKey))})";
        }

        string Describe(byte[] bytes) =>
            _repo.Format is JsonSaveFormat && IsUtf8Text(bytes)
                ? Encoding.UTF8.GetString(bytes)
                : HexSummary(bytes);

        static bool IsUtf8Text(byte[] bytes)
        {
            try
            {
                new UTF8Encoding(false, true).GetString(bytes);
                return true;
            }
            catch (DecoderFallbackException)
            {
                return false;
            }
        }

        static string HexSummary(byte[] bytes)
        {
            var text = new StringBuilder($"{bytes.Length:N0} bytes of binary data\n\n");
            int shown = Math.Min(bytes.Length, HexPreviewBytes);
            for (int i = 0; i < shown; i++)
            {
                text.Append(bytes[i].ToString("X2"));
                text.Append(i % 16 == 15 ? '\n' : ' ');
            }

            if (shown < bytes.Length)
                text.Append($"\n… {bytes.Length - shown:N0} more bytes");
            return text.ToString();
        }

        static TextField TextView(string text)
        {
            var field = new TextField { multiline = true, isReadOnly = true, value = text, style = { flexGrow = 1, whiteSpace = WhiteSpace.Normal } };
            field.AddToClassList("unity-base-text-field--vertical-scrolling");
            return field;
        }

        static Label MutedLabel(string text) =>
            new(text) { style = { color = new Color(1, 1, 1, 0.5f) } };
    }
}
