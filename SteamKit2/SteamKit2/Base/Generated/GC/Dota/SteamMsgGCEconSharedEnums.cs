// <auto-generated>
//   This file was generated by a tool; you should avoid making direct changes.
//   Consider using 'partial classes' to extend these types
//   Input: econ_shared_enums.proto
// </auto-generated>

#region Designer generated code
#pragma warning disable CS0612, CS0618, CS1591, CS3021, CS8981, IDE0079, IDE1006, RCS1036, RCS1057, RCS1085, RCS1192
namespace SteamKit2.GC.Dota.Internal
{

    [global::ProtoBuf.ProtoContract()]
    public partial class CMsgGenericResult : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1)]
        [global::System.ComponentModel.DefaultValue(2u)]
        public uint eresult
        {
            get => __pbn__eresult ?? 2u;
            set => __pbn__eresult = value;
        }
        public bool ShouldSerializeeresult() => __pbn__eresult != null;
        public void Reseteresult() => __pbn__eresult = null;
        private uint? __pbn__eresult;

        [global::ProtoBuf.ProtoMember(2)]
        [global::System.ComponentModel.DefaultValue("")]
        public string debug_message
        {
            get => __pbn__debug_message ?? "";
            set => __pbn__debug_message = value;
        }
        public bool ShouldSerializedebug_message() => __pbn__debug_message != null;
        public void Resetdebug_message() => __pbn__debug_message = null;
        private string __pbn__debug_message;

    }

    [global::ProtoBuf.ProtoContract()]
    public enum EGCEconBaseMsg
    {
        k_EMsgGCGenericResult = 2579,
    }

    [global::ProtoBuf.ProtoContract()]
    public enum EGCMsgResponse
    {
        k_EGCMsgResponseOK = 0,
        k_EGCMsgResponseDenied = 1,
        k_EGCMsgResponseServerError = 2,
        k_EGCMsgResponseTimeout = 3,
        k_EGCMsgResponseInvalid = 4,
        k_EGCMsgResponseNoMatch = 5,
        k_EGCMsgResponseUnknownError = 6,
        k_EGCMsgResponseNotLoggedOn = 7,
        k_EGCMsgFailedToCreate = 8,
    }

    [global::ProtoBuf.ProtoContract()]
    public enum EGCMsgUseItemResponse
    {
        k_EGCMsgUseItemResponse_ItemUsed = 0,
        k_EGCMsgUseItemResponse_GiftNoOtherPlayers = 1,
        k_EGCMsgUseItemResponse_ServerError = 2,
        k_EGCMsgUseItemResponse_MiniGameAlreadyStarted = 3,
        k_EGCMsgUseItemResponse_ItemUsed_ItemsGranted = 4,
        k_EGCMsgUseItemResponse_DropRateBonusAlreadyGranted = 5,
        k_EGCMsgUseItemResponse_NotInLowPriorityPool = 6,
        k_EGCMsgUseItemResponse_NotHighEnoughLevel = 7,
        k_EGCMsgUseItemResponse_EventNotActive = 8,
        k_EGCMsgUseItemResponse_ItemUsed_EventPointsGranted = 9,
        k_EGCMsgUseItemResponse_MissingRequirement = 10,
        k_EGCMsgUseItemResponse_EmoticonUnlock_NoNew = 11,
        k_EGCMsgUseItemResponse_EmoticonUnlock_Complete = 12,
        k_EGCMsgUseItemResponse_ItemUsed_Compendium = 13,
    }

}

#pragma warning restore CS0612, CS0618, CS1591, CS3021, CS8981, IDE0079, IDE1006, RCS1036, RCS1057, RCS1085, RCS1192
#endregion
