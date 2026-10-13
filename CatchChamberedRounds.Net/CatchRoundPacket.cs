using Fika.Core.Networking.LiteNetLib.Utils;

namespace Tosox.CatchChamberedRounds.Net
{
    public struct CatchRoundPacket : INetSerializable
    {
        public const byte KindCatch = 0;

        public byte Kind;
        public string Version;
        public string ProfileId;
        public string RoundId;

        public static CatchRoundPacket Create(string version, string profileId, string roundId)
        {
            return new CatchRoundPacket
            {
                Kind = KindCatch,
                Version = version,
                ProfileId = profileId,
                RoundId = roundId
            };
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Kind);
            writer.Put(Version ?? string.Empty);
            writer.Put(ProfileId ?? string.Empty);
            writer.Put(RoundId ?? string.Empty);
        }

        public void Deserialize(NetDataReader reader)
        {
            Kind = reader.GetByte();
            Version = reader.GetString();
            ProfileId = reader.GetString();
            RoundId = reader.GetString();
        }
    }
}
