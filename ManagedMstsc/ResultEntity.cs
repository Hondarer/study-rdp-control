using MSTSCLib;
using System.Text.Json.Serialization;

namespace ManagedMstsc
{
    public class ResultEntity
    {
        [JsonPropertyName("disconnectReason")]
        public int DisconnectReason { get; set; }

        [JsonIgnore]
        public ExtendedDisconnectReasonCode ExtendedDisconnectReason { get; set; }

        [JsonPropertyName("extendedDisconnectReason")]
        public string ExtendedDisconnectReasonString
        {
            get
            {
                return ExtendedDisconnectReason.ToString();
            }
        }

        /// <summary>
        /// 切断理由を取得します。
        /// </summary>
        /// <remarks>
        /// <see cref="DisconnectReason"/> および <see cref="ExtendedDisconnectReason"/> が 0 であっても、
        /// 本値が <see cref="string.IsNullOrEmpty(string?)"/> でない場合はエラーとして扱います。
        /// </remarks>
        [JsonPropertyName("disconnectReasonString")]
        public string DisconnectReasonString { get; set; } = null;

        public ResultEntity(string disconnectReasonString, int disconnectReason = 0, ExtendedDisconnectReasonCode extendedDisconnectReason = ExtendedDisconnectReasonCode.exDiscReasonNoInfo)
        {
            DisconnectReason = disconnectReason;
            ExtendedDisconnectReason = extendedDisconnectReason;
            DisconnectReasonString = disconnectReasonString;
        }

        [JsonPropertyName("isError")]
        public bool IsError
        {
            get
            {
                // DisconnectReason
                // https://docs.microsoft.com/en-us/windows/win32/termserv/imstscaxevents-ondisconnected
                //
                // 1 - Local disconnection. This is not an error code.
                // 2 - Remote disconnection by user. This is not an error code.
                // 3 - Remote disconnection by server. This is not an error code.

                if ((DisconnectReason == 1) || (DisconnectReason == 2) || (DisconnectReason == 3))
                {
                    return false;
                }

                return string.IsNullOrEmpty(DisconnectReasonString) == false;
            }
        }
    }
}
