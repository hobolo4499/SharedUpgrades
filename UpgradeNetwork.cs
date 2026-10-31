using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace SharedUpgrades;

// Host -> clients: "give player X n levels of upgrade Y". Clients apply it locally so the
// immediate effects (max health, sprint speed, ...) happen on every machine, like vanilla.
internal static class UpgradeNetwork
{
    // The game itself uses event codes 123 and 124.
    private const byte GrantEventCode = 178;

    internal static void Init()
    {
        PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
    }

    internal static void SendGrant(string steamID, string upgrade, int amount)
    {
        if (!SemiFunc.IsMultiplayer())
        {
            return;
        }

        PhotonNetwork.RaiseEvent(
            GrantEventCode,
            new object[] { steamID, upgrade, amount },
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable);
    }

    private static void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != GrantEventCode || PhotonNetwork.IsMasterClient)
        {
            return;
        }
        if (PhotonNetwork.MasterClient == null || photonEvent.Sender != PhotonNetwork.MasterClient.ActorNumber)
        {
            return;
        }
        if (photonEvent.CustomData is not object[] { Length: 3 } data
            || data[0] is not string steamID
            || data[1] is not string upgrade
            || data[2] is not int amount)
        {
            return;
        }

        UpgradeLedger.Apply(steamID, upgrade, amount);
    }
}
