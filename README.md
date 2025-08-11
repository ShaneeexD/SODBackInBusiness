# BackInBusiness Mod

## Overview
BackInBusiness is a mod for Shadows of Doubt that aims to expand the business management aspects of the game. Players can purchase, own, and manage various businesses.

## Current Features
*   **Business Ownership:** Players can acquire businesses within the game.
*   **Per-Save Data Storage:** Business ownership data is saved uniquely for each game save file (e.g., `businesses_MySaveGame.json`), located in the mod's data directory (`<Game_Data>/Mods/BackInBusiness/Data/`).
*   **Save on Game Save:** Business data is saved automatically when the game performs a save operation (e.g., sleeping, new day), ensuring data integrity with the game's state.
*   **Data Serialization:** Uses `SimpleJSON` for robust and self-contained JSON data handling.

## Installation
1.  Ensure BepInEx (IL2CPP version for Shadows of Doubt) is installed.
2.  Place the `BackInBusiness.dll` file into your `BepInEx/plugins` folder.

## Usage
(Details on how to purchase and manage businesses in-game to be added as features are finalized.)

## Current Status
*   Core business purchasing and data persistence logic is implemented.
*   Requires thorough testing of the save/load functionality across various game scenarios.

## Future Plans
*   Income generation and collection from owned businesses.
*   Employee management (hiring/firing).
*   Business upgrade system.
*   Integration with an in-game Cruncher app for business management (currently on hold).

(This README is a work in progress and will be updated as development continues.)
