using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace OfflineCompanionsAI
{
    /// <summary>Cliente HTTP. Usa corrutinas de Unity, así que no bloquea el juego mientras espera.</summary>
    public sealed class LlmClient
    {
        private readonly ModSettings _settings;

        public LlmClient(ModSettings settings)
        {
            _settings = settings;
        }

        /// <summary>Peticiones de IA desde que se abrió el juego, para que el jugador pueda vigilar el gasto.</summary>
        public int RequestsThisRun { get; private set; }

        /// <summary>El saldo de Player2. No gasta nada.</summary>
        public IEnumerator FetchJoules(Action<long?> onResult)
        {
            using (UnityWebRequest req = UnityWebRequest.Get(_settings.VoiceBaseUrl.Value.TrimEnd('/') + "/joules"))
            {
                req.timeout = 5;
                req.SetRequestHeader("player2-game-key", ModSettings.GameKey);
                yield return req.SendWebRequest();
                onResult(req.result == UnityWebRequest.Result.Success ? LlmProtocol.ParseJoules(req.downloadHandler?.text) : null);
            }
        }

        public IEnumerator Complete(IEnumerable<ChatMessage> messages, JArray tools, Action<LlmResponse> onResponse, Action<string> onError)
        {
            RequestsThisRun++;
            string body = LlmProtocol.BuildRequest(messages, _settings.ResolvedModel, _settings.MaxTokens.Value, _settings.Temperature.Value, tools);
            using (var req = new UnityWebRequest(_settings.ResolvedBaseUrl + "/chat/completions", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = _settings.TimeoutSeconds.Value;
                req.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                ApplyAuth(req);

                yield return req.SendWebRequest();

                string text = req.downloadHandler?.text;
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Plugin.Log.LogWarning("[AI] Request failed: HTTP " + req.responseCode + " - " + req.error);
                    onError(LlmProtocol.DescribeError(req.responseCode, req.error, text));
                    yield break;
                }

                LlmResponse response = LlmProtocol.ParseResponse(text);
                if (response == null)
                {
                    Plugin.Log.LogWarning("[AI] Response without text or actions.");
                    onError(ModLang.Pick(
                        "La IA no devolvió texto. Si usas MaxTokens, prueba a subirlo o a ponerlo en 0.",
                        "The AI returned no text. If you use MaxTokens, try raising it or setting it to 0."));
                    yield break;
                }
                Plugin.Log.LogInfo("[AI] Response received: " + (response.Text?.Length ?? 0) + " chars, " + response.ToolCalls.Count + " actions.");
                onResponse(response);
            }
        }

        /// <summary>Pide a la app de Player2 que empiece a escuchar el micrófono. Siempre con Player2, sea cual sea el proveedor del chat.</summary>
        public IEnumerator StartListening(int seconds, Action onStarted, Action<string> onError)
        {
            using (var req = new UnityWebRequest(_settings.VoiceBaseUrl.Value.TrimEnd('/') + "/stt/start", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(VoiceProtocol.StartBody(seconds)));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = _settings.TimeoutSeconds.Value;
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("player2-game-key", ModSettings.GameKey);

                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Plugin.Log.LogWarning("[Voice] Could not start: HTTP " + req.responseCode + " - " + req.error);
                    onError(VoiceProtocol.DescribeError(req.responseCode, req.downloadHandler?.text));
                    yield break;
                }
                onStarted();
            }
        }

        /// <summary>Termina la escucha y devuelve el texto reconocido, o null si no se entendió nada.</summary>
        public IEnumerator StopListening(Action<string> onText, Action<string> onError)
        {
            using (var req = new UnityWebRequest(_settings.VoiceBaseUrl.Value.TrimEnd('/') + "/stt/stop", "POST"))
            {
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = _settings.TimeoutSeconds.Value;
                req.SetRequestHeader("player2-game-key", ModSettings.GameKey);

                yield return req.SendWebRequest();

                string body = req.downloadHandler?.text;
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Plugin.Log.LogWarning("[Voice] Could not stop: HTTP " + req.responseCode + " - " + req.error);
                    onError(VoiceProtocol.DescribeError(req.responseCode, body));
                    yield break;
                }

                string text = VoiceProtocol.ParseText(body);
                Plugin.Log.LogInfo("[Voice] Recognized " + (text?.Length ?? 0) + " chars.");
                onText(text);
            }
        }

        /// <summary>Player2 pide esta señal cada 60 segundos para atribuir el uso al juego.</summary>
        public IEnumerator PingHealth()
        {
            using (UnityWebRequest req = UnityWebRequest.Get(_settings.ResolvedBaseUrl + "/health"))
            {
                req.timeout = 5;
                ApplyAuth(req);
                yield return req.SendWebRequest();
            }
        }

        private void ApplyAuth(UnityWebRequest req)
        {
            if (_settings.Provider.Value == AiProvider.Player2)
            {
                req.SetRequestHeader("player2-game-key", ModSettings.GameKey);
            }
            string key = _settings.ApiKey.Value;
            if (!string.IsNullOrWhiteSpace(key))
            {
                req.SetRequestHeader("Authorization", "Bearer " + key.Trim());
            }
        }
    }
}
