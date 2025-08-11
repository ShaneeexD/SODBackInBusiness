# Project Notes - BackInBusiness

## Newtonsoft.Json.dll Version Mismatch Issue (Superseded by SimpleJSON)

**Symptom:**
Error during game runtime: `Method not found: 'Void Newtonsoft.Json.JsonSerializerSettings..ctor()'` when trying to serialize/deserialize data, typically in `SaveBusinessData()` or `LoadBusinessData()`.

**Cause:**
This error occurs because the version of `Newtonsoft.Json.dll` your mod was compiled against (e.g., from a NuGet package) is different from the version loaded by the game or BepInEx at runtime. The specific constructor or method your code is trying to use (like the parameterless `JsonSerializerSettings()` constructor) doesn't exist or isn't accessible in the runtime version.

**Solution:**
1.  **Remove NuGet Reference:** In your mod's `.csproj` file, remove any NuGet package reference to `Newtonsoft.Json`.
2.  **Reference Game/BepInEx DLL:**
    *   Locate the `Newtonsoft.Json.dll` used by the game or BepInEx. Common locations:
        *   Game's `Managed` folder (e.g., `ShadowOfMordor_Data/Managed/`)
        *   BepInEx folders (e.g., `BepInEx/core/`, `BepInEx/unhollowed/`)
    *   Add a direct file reference to this DLL in your `.csproj` file. Example:
        ```xml
        <Reference Include="Newtonsoft.Json">
          <HintPath>C:\Path\To\GameOrBepInEx\Newtonsoft.Json.dll</HintPath>
          <Private>False</Private> <!-- Important: Prevents copying the DLL to your mod's output -->
        </Reference>
        ```
    *   Setting `<Private>False</Private>` ensures your mod uses the DLL already present in the game environment and doesn't bundle its own potentially conflicting copy.
3.  **Recompile:** Clean and rebuild your mod project.

This ensures your mod is compiled using the same API surface for `Newtonsoft.Json` that will be available at runtime, resolving the "Method not found" errors and ensuring reliable data serialization.

**Update:** This issue has been superseded by the decision to use the `SimpleJSON.cs` library included with the mod for all JSON serialization and deserialization tasks. This removes the dependency on external `Newtonsoft.Json.dll` versions and provides more direct control over JSON handling.

## Save/Load Logic Overhaul (October 2023 - May 2024)

*   **Per-Save File Data:** Business data is now saved to a unique JSON file for each game save (e.g., `businesses_MySaveGame.json`). This prevents data from one save file affecting another.
    *   Implemented `GetCurrentSaveFileName()` in `BusinessManager.cs` to retrieve and sanitize the current save game's name.
    *   Implemented `GetBusinessDataFilePath()` in `BusinessManager.cs` to construct the full path for the save-specific data file.
*   **Save on Game Save Event:** Business data is no longer saved automatically when a business is purchased. Instead, `BusinessManager.Instance.SaveBusinessData()` is now triggered by the `Lib.SaveGame.OnAfterSave` event from `SOD.Common`. This ensures data is saved only when the game itself performs a save operation.
    *   Subscription to this event is handled in `PatchClass.cs`.
*   **Serialization with SimpleJSON:** The mod now uses the embedded `SimpleJSON.cs` library for serializing `OwnedBusinessData` to JSON and deserializing it. This replaced the previous attempts to use `Newtonsoft.Json` and resolved associated versioning/compatibility issues.
*   **Directory Creation:** The `SaveBusinessData()` method now ensures that the `Data` directory (and any necessary parent directories like `BackInBusiness/Data`) exists before attempting to write the file.

## Other Notes

*   Investigated Cruncher app integration. The primary blocker seemed to be the lack of an AssetBundle for the `CruncherAppPreset` and its UI. The `PiePie RealEstate` mod uses this approach successfully. Forcing the app to load via `currentApp` assignment showed UI rendering issues (magenta screen due to missing materials) and missing icons, which are also better handled via asset bundles.
