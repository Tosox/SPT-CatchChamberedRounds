using Fika.Core.Networking.LiteNetLib.Utils;

namespace Tosox.CatchChamberedRounds.Net
{
    public struct CatchRoundPacket : INetSerializable
    {
        public const byte KindCatch = 0;
        public const byte KindAnimationCue = 1;
        public const byte KindReloadAborted = 2;

        public byte Kind;
        public string Version;
        public string ProfileId;
        public string RoundId;
        public bool FastHide;

        public static CatchRoundPacket Create(byte kind, string version, string profileId, string roundId,
            bool fastHide = false)
        {
            return new CatchRoundPacket
            {
                Kind = kind,
                Version = version,
                ProfileId = profileId,
                RoundId = roundId,
                FastHide = fastHide
            };
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Kind);
            writer.Put(Version ?? string.Empty);
            writer.Put(ProfileId ?? string.Empty);
            writer.Put(RoundId ?? string.Empty);
            writer.Put(FastHide);
        }

        public void Deserialize(NetDataReader reader)
        {
            Kind = reader.GetByte();
            Version = reader.GetString();
            ProfileId = reader.GetString();
            RoundId = reader.GetString();
            FastHide = reader.GetBool();
        }
    }
}
