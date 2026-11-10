using Rivalia.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Rivalia.EditorTools
{
    public static class RivaliaSceneMenu
    {
        private const string ScenePath = "Assets/_Rivalia/Scenes/Arena_ValeQuatroCoroas.unity";

        /// <summary>Cria (ou recria) a cena do MVP, salva e coloca nas Build Settings.</summary>
        [MenuItem("Rivalia/Criar cena MVP")]
        public static void CreateMvpScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Rivalia Bootstrap").AddComponent<RivaliaBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("Cena do MVP criada em " + ScenePath + ". Aperte Play.");
        }

        [MenuItem("Rivalia/Configurar projeto para mobile")]
        public static void ConfigureProject()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.companyName = "Rivalia";
            PlayerSettings.productName = "Rivalia";
            Debug.Log("Orientação paisagem configurada. Em Player > Other Settings, confirme Active Input Handling = Input System Package (New) ou Both.");
        }
    }
}
