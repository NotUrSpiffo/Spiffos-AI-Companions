using System;
using UnityEngine;

namespace OfflineCompanionsAI
{
    /// <summary>
    /// Lo que dice un compañero solo se dibuja en el juego de su dueño. Con ShareSpeech, se envía también a los demás
    /// jugadores, y cada juego que tenga este mod lo muestra como si lo dijera un jugador: en burbuja y en el chat, y solo
    /// si está a la distancia a la que se oye hablar normal en el chat de Valheim.
    /// </summary>
    internal static class SharedSpeech
    {
        // El mismo nombre en las dos compilaciones (la del usuario y la de instalación nueva), para que se entiendan.
        private const string RpcName = "SpiffoAiCompanions_Say";
        private const float DefaultHearing = 15f;
        private const byte KindBubble = 0;
        private const byte KindChat = 1;

        /// <summary>Escribe una línea de otro jugador en el chat local, con las mismas reglas que las propias.</summary>
        public static Action<string, string> ChatLine;

        private static ZRoutedRpc _registeredOn;

        /// <summary>Cada partida crea su propio ZRoutedRpc: hay que registrarse en cada una.</summary>
        public static void Update()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || rpc == _registeredOn) return;
            rpc.Register<ZPackage>(RpcName, Receive);
            _registeredOn = rpc;
        }

        public static void SendBubble(Component companion, string text, float seconds)
        {
            Send(companion, KindBubble, CompanionBridge.Name(companion), text, seconds);
        }

        public static void SendChat(Component companion, string name, string text)
        {
            Send(companion, KindChat, name, text, 0f);
        }

        private static void Send(Component companion, byte kind, string name, string text, float seconds)
        {
            if (companion == null || string.IsNullOrEmpty(text) || ZRoutedRpc.instance == null || _registeredOn != ZRoutedRpc.instance) return;
            if (ZNet.instance == null || ZNet.instance.GetPeers().Count == 0) return;
            ZNetView view = companion.GetComponent<ZNetView>();
            if (view == null || !view.IsValid()) return;
            var package = new ZPackage();
            package.Write(view.GetZDO().m_uid);
            package.Write(kind);
            package.Write(name ?? "");
            package.Write(text);
            package.Write(seconds);
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, RpcName, package);
        }

        private static void Receive(long sender, ZPackage package)
        {
            try
            {
                if (sender == ZDOMan.GetSessionID()) return;
                ZDOID id = package.ReadZDOID();
                byte kind = package.ReadByte();
                string name = package.ReadString();
                string text = package.ReadString();
                float seconds = package.ReadSingle();

                Player player = Player.m_localPlayer;
                GameObject go = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(id) : null;
                if (player == null || go == null) return;
                if (Vector3.Distance(player.transform.position, go.transform.position) > HearingDistance(player)) return;

                Component companion = go.GetComponent<Character>();
                if (companion == null) return;
                if (kind == KindBubble) CompanionBridge.Say(companion, text, seconds);
                else if (kind == KindChat) ChatLine?.Invoke(name, text);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning("[Multijugador] " + ModLang.Pick(
                    "No se pudo mostrar lo que dijo un compañero de otro jugador: ",
                    "Could not show what another player's companion said: ",
                    "Не удалось отобразить реплику спутника другого игрока: ") + ex.Message);
            }
        }

        /// <summary>La distancia a la que se oye hablar normal en el chat de Valheim.</summary>
        private static float HearingDistance(Player player)
        {
            Talker talker = player.GetComponent<Talker>();
            return talker != null && talker.m_normalDistance > 0f ? talker.m_normalDistance : DefaultHearing;
        }
    }
}
