using System.Net.Http.Headers;
using Frontend_Mobile.Data;
using Frontend_Mobile.Models;
using QULOOBUL__MOE_MIENEEN_Shared.Files;
using static QULOOBUL__MOE_MIENEEN_Shared.Classes.Common;
using Permissions = QULOOBUL__MOE_MIENEEN_Shared.Classes.Common.Permissions;

namespace Frontend_Mobile.Pages;

/// <summary>UC-07 / SD-04 - the mobile equivalent of Files.razor.</summary>
public partial class FilesPage : ContentPage
{
    // Matches Files:MaxBytes' default in appsettings.json - purely an early client-side check;
    // the API re-validates authoritatively before it ever issues an upload SAS.
    private const long MaxBytes = 25L * 1024 * 1024;

    private static readonly Dictionary<string, string> ExtensionContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
    };

    // Mirrors FileUploadService's DefaultExtensions, per platform. Purely a picker filter -
    // the API is still the one that actually enforces which extensions are allowed.
    private static readonly FilePickerFileType AllowedFileTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
    {
        [DevicePlatform.iOS] =
        [
            "com.adobe.pdf", "com.microsoft.word.doc", "org.openxmlformats.wordprocessingml.document",
            "com.microsoft.excel.xls", "org.openxmlformats.spreadsheetml.sheet",
            "com.microsoft.powerpoint.ppt", "org.openxmlformats.presentationml.presentation",
            "public.plain-text", "public.comma-separated-values-text",
            "public.png", "public.jpeg", "com.compuserve.gif", "org.webmproject.webp",
        ],
        [DevicePlatform.Android] =
        [
            "application/pdf", "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.ms-powerpoint", "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "text/plain", "text/csv", "image/png", "image/jpeg", "image/gif", "image/webp",
        ],
        [DevicePlatform.WinUI] =
        [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".png", ".jpg", ".jpeg", ".gif", ".webp"],
    });

    private readonly AppState _state = AppState.Current;
    private List<FileCategoryDto> _categories = [];
    private FileResult? _picked;
    private CancellationTokenSource? _searchDebounce;
    // Bumped per Load(); a response that comes back after a newer Load() started is dropped,
    // so a slow earlier search can't overwrite the results of a later one.
    private int _loadVersion;

    private bool CanManage => _state.HasPermission(Permissions.ManageFiles);

    public FilesPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ShowArchivedRow.IsVisible = CanManage;
        // Once only - re-setting the pickers' ItemsSource would wipe the user's selections.
        if (_categories.Count == 0) await LoadCategories();
        await Load();
    }

    // ----- Loading -----

    private async Task LoadCategories()
    {
        _categories = await _state.GetFileCategoriesAsync() ?? [];

        CategoryPicker.ItemsSource = _categories;
        CategoryPicker.ItemDisplayBinding = new Binding(nameof(FileCategoryDto.CategoryName));

        var filterOptions = new List<Option<int?>> { new(null, "All categories") };
        filterOptions.AddRange(_categories.Select(c => new Option<int?>(c.CategoryId, c.CategoryName)));
        FilterCategoryPicker.ItemsSource = filterOptions;
        FilterCategoryPicker.ItemDisplayBinding = new Binding(nameof(Option<int?>.Label));
        // No SelectedIndex = 0 here: it would fire OnFilterChanged and load the list a second
        // time. Unselected already means "all categories" (SelectedItem is null).
    }

    private async Task Load()
    {
        var version = ++_loadVersion;
        Busy.IsVisible = true;
        Busy.IsRunning = true;

        var filterCategoryId = (FilterCategoryPicker.SelectedItem as Option<int?>)?.Value;
        var includeArchived = CanManage && ShowArchivedSwitch.IsToggled;

        var files = await _state.ListFilesAsync(includeArchived, SearchEntry.Text, filterCategoryId) ?? [];
        if (version != _loadVersion) return; // a newer Load() owns the list and the spinner now

        var rows = files.Select(f => new FileRow(f, CanManage)).ToList();
        BindableLayout.SetItemsSource(FileList, rows);
        FileList.IsVisible = rows.Count > 0;
        EmptyLabel.IsVisible = rows.Count == 0;

        Busy.IsVisible = false;
        Busy.IsRunning = false;
    }

    private async void OnFilterChanged(object? sender, EventArgs e) => await Load();

    private async void OnShowArchivedToggled(object? sender, ToggledEventArgs e) => await Load();

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        // 300ms debounce, matching Files.razor's DebounceInterval - avoids a request per keystroke.
        _searchDebounce?.Cancel();
        var cts = new CancellationTokenSource();
        _searchDebounce = cts;
        _ = DebouncedSearchAsync(cts.Token);
    }

    private async Task DebouncedSearchAsync(CancellationToken token)
    {
        try { await Task.Delay(300, token); }
        catch (TaskCanceledException) { return; }
        if (!token.IsCancellationRequested) await Load();
    }

    // ----- Upload -----

    private void OnUploadFormChanged(object? sender, EventArgs e) => UpdateUploadState();

    private void UpdateUploadState() =>
        UploadButton.IsEnabled = _picked is not null && CategoryPicker.SelectedItem is FileCategoryDto;

    private async void OnChooseFile(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose a file",
                FileTypes = AllowedFileTypes,
            });
            if (result is null) return;

            _picked = result;
            PickedFileLabel.Text = result.FileName;
            UpdateUploadState();
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Couldn't open file picker", "Try again.", "OK");
        }
    }

    private async void OnUpload(object? sender, EventArgs e)
    {
        if (_picked is not { } picked || CategoryPicker.SelectedItem is not FileCategoryDto category) return;

        UploadButton.IsEnabled = false;
        Busy.IsVisible = true;
        Busy.IsRunning = true;
        try
        {
            using var stream = await picked.OpenReadAsync();
            var size = stream.Length;
            if (size > MaxBytes)
            {
                await DisplayAlertAsync("Too large", $"That file is larger than {MaxBytes / (1024 * 1024)} MB.", "OK");
                return;
            }

            var contentType = string.IsNullOrEmpty(picked.ContentType) ? GuessContentType(picked.FileName) : picked.ContentType;

            var ticket = await _state.InitiateFileUploadAsync(new FileUploadInitRequest
            {
                FileName = picked.FileName,
                FileType = contentType,
                SizeBytes = size,
                CategoryId = category.CategoryId,
            });
            if (ticket is null) return;

            // The SAS in the URL is the credential; the API's bearer token must never reach
            // Blob Storage, so this is a fresh, unauthenticated HttpClient - never _state's own.
            using var blobClient = new HttpClient();
            using var content = new StreamContent(stream);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            using var put = new HttpRequestMessage(HttpMethod.Put, AppState.ForThisDevice(new Uri(ticket.UploadUrl))) { Content = content };
            put.Headers.Add("x-ms-blob-type", "BlockBlob");

            using var res = await blobClient.SendAsync(put);
            if (res.IsSuccessStatusCode)
            {
                _picked = null;
                PickedFileLabel.Text = $"Uploaded {picked.FileName}";
            }
            else
            {
                await DisplayAlertAsync("Upload failed", $"Storage refused the upload ({(int)res.StatusCode}).", "OK");
            }
            // Either way: the API recorded the metadata row before the PUT, so even a failed
            // upload left a row behind - show it rather than hide it (same as Files.razor).
            await Load();
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Upload failed", "Couldn't read or send that file.", "OK");
        }
        finally
        {
            UpdateUploadState();
            Busy.IsVisible = false;
            Busy.IsRunning = false;
        }
    }

    private static string GuessContentType(string fileName) =>
        ExtensionContentTypes.TryGetValue(Path.GetExtension(fileName), out var type) ? type : "application/octet-stream";

    // ----- Row actions -----

    private async void OnDownload(object? sender, EventArgs e)
    {
        if ((sender as Element)?.BindingContext is not FileRow row) return;
        if (await _state.GetFileDownloadLinkAsync(row.Dto.FileId) is not { } link) return;

        try { await Launcher.Default.OpenAsync(AppState.ForThisDevice(new Uri(link.DownloadUrl))); }
        catch (Exception) { await DisplayAlertAsync("Couldn't open", "The download link couldn't be opened automatically.", "OK"); }
    }

    private async void OnRecategorise(object? sender, EventArgs e)
    {
        if ((sender as Element)?.BindingContext is not FileRow row) return;

        var names = _categories.Select(c => c.CategoryName).ToArray();
        var choice = await DisplayActionSheetAsync("Move to category", "Cancel", null, names);
        if (choice is null || choice == "Cancel") return;

        var category = _categories.FirstOrDefault(c => c.CategoryName == choice);
        if (category is null) return;

        if (await _state.RecategoriseFileAsync(row.Dto.FileId, category.CategoryId))
            await Load();
    }

    private async void OnToggleArchive(object? sender, EventArgs e)
    {
        if ((sender as Element)?.BindingContext is not FileRow row) return;
        if (await _state.SetFileArchivedAsync(row.Dto.FileId, !row.IsArchived))
            await Load();
    }

    private async void OnDelete(object? sender, EventArgs e)
    {
        if ((sender as Element)?.BindingContext is not FileRow row) return;
        if (!await DisplayAlertAsync("Delete file", $"Delete \"{row.FileName}\" permanently?", "Delete", "Cancel")) return;

        if (await _state.DeleteFileAsync(row.Dto.FileId))
            await Load();
    }
}
