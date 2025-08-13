using SOD.Common;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityStandardAssets.Characters.FirstPerson;

namespace BackInBusiness
{
    public class NewGameHandler : MonoBehaviour
    {
        public static MurderController.Murder murder;
        public static Toolbox toolbox;
        private GameObject cube;

        public bool atMenu = true;

        public NewGameHandler()
        {
            Lib.SaveGame.OnAfterLoad += HandleGameLoaded;
            Lib.SaveGame.OnAfterNewGame += HandleNewGameStarted;
        }

        private void HandleNewGameStarted(object sender, EventArgs e)
        {
            atMenu = false;
        }

        private void HandleGameLoaded(object sender, EventArgs e)
        {
            atMenu = false;
        }
    }
}