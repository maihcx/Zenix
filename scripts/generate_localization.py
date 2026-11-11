# Copyright (c) Zenix. All rights reserved.
# Licensed under the proprietary and confidential license.

import os

KEYS_DATA = [
    # Shell & Navigation
    ("Shell_Title", "Zenix", "Zenix"),
    ("Shell_Nav_Chats", "Chats", "Tin nhắn"),
    ("Shell_Nav_Contacts", "Contacts", "Danh bạ"),
    ("Shell_Nav_Settings", "Settings", "Cài đặt"),

    # Login & QR Code
    ("Login_Title", "Login to Zenix", "Đăng nhập Zenix"),
    ("Login_Subtitle", "Scan QR code with your mobile app to sign in", "Quét mã QR bằng ứng dụng trên điện thoại để đăng nhập"),
    ("Login_ScanInstruction", "Open mobile app > Scan QR Code", "Mở ứng dụng trên điện thoại > Quét mã QR"),
    ("Login_WaitingScan", "Waiting for scan...", "Đang chờ quét..."),
    ("Login_WaitingConfirm", "Scanned! Please confirm on your phone.", "Đã quét! Vui lòng xác nhận trên điện thoại."),
    ("Login_ConfirmPrompt", "Tap Log in on your phone to continue", "Nhấn Đăng nhập trên điện thoại để tiếp tục"),
    ("Login_SecondQrRequired", "Device Linking Confirmation Required", "Yêu cầu xác nhận liên kết thiết bị"),
    ("Login_SecondQrInstruction", "Please scan this secondary QR code to verify your new device", "Vui lòng quét mã QR thứ hai này để xác thực thiết bị mới"),
    ("Login_Expired", "QR code has expired", "Mã QR đã hết hạn"),
    ("Login_RefreshButton", "Refresh QR Code", "Lấy mã mới"),
    ("Login_SwitchAccount", "Switch Account", "Chuyển tài khoản"),
    ("Login_KickedOutNotice", "Your session has ended because you logged in from another location", "Phiên đăng nhập đã kết thúc do bạn đăng nhập từ nơi khác"),

    # Conversation List (Sidebar)
    ("Conversation_SearchPlaceholder", "Search conversations and messages...", "Tìm kiếm hội thoại và tin nhắn..."),
    ("Conversation_EmptyList", "No conversations yet", "Chưa có cuộc trò chuyện nào"),
    ("Conversation_Pin", "Pin to top", "Ghim lên đầu"),
    ("Conversation_Unpin", "Unpin", "Bỏ ghim"),
    ("Conversation_Mute", "Mute notifications", "Tắt thông báo"),
    ("Conversation_Unmute", "Unmute notifications", "Bật thông báo"),
    ("Conversation_Delete", "Delete conversation", "Xóa cuộc trò chuyện"),
    ("Conversation_MarkAsRead", "Mark as read", "Đánh dấu đã đọc"),
    ("Conversation_Archive", "Archive", "Lưu trữ"),

    # Chat Active Viewport & Composer
    ("Chat_ComposerPlaceholder", "Type a message...", "Nhập tin nhắn..."),
    ("Chat_SendButton", "Send", "Gửi"),
    ("Chat_AttachFileTooltip", "Attach file or photo", "Đính kèm tệp hoặc ảnh"),
    ("Chat_ReplyingTo", "Replying to", "Đang trả lời"),
    ("Chat_CancelReply", "Cancel reply", "Hủy trả lời"),
    ("Chat_RecallMessage", "Recall message", "Thu hồi tin nhắn"),
    ("Chat_DeleteMessage", "Delete message", "Xóa tin nhắn"),
    ("Chat_UndoDelete", "Undo", "Hoàn tác"),
    ("Chat_UndoCountdown", "Message deleted. Undo ({0}s)", "Đã xóa tin nhắn. Hoàn tác ({0}s)"),
    ("Chat_RecalledNotice", "Message has been recalled", "Tin nhắn đã được thu hồi"),
    ("Chat_LoadingHistory", "Loading older messages...", "Đang tải tin nhắn cũ hơn..."),
    ("Chat_SelectConversationPrompt", "Select a conversation to start messaging", "Chọn một cuộc trò chuyện để bắt đầu nhắn tin"),

    # Delivery Status
    ("Status_Sending", "Sending...", "Đang gửi..."),
    ("Status_Sent", "Sent", "Đã gửi"),
    ("Status_Delivered", "Delivered", "Đã nhận"),
    ("Status_Seen", "Seen", "Đã xem"),
    ("Status_Failed", "Failed to send", "Gửi thất bại"),

    # Media & File Transfer
    ("Media_Uploading", "Uploading...", "Đang tải lên..."),
    ("Media_Downloading", "Downloading...", "Đang tải xuống..."),
    ("Media_DownloadComplete", "Download complete", "Tải xuống hoàn tất"),
    ("Media_UploadFailed", "Upload failed", "Tải lên thất bại"),
    ("Media_ExtractPoster", "Extracting video preview...", "Đang trích xuất ảnh xem trước..."),

    # Common Dialogs & Pickers
    ("Dialog_Ok", "OK", "Đồng ý"),
    ("Dialog_Cancel", "Cancel", "Hủy"),
    ("Dialog_Yes", "Yes", "Có"),
    ("Dialog_No", "No", "Không"),
    ("Dialog_DeleteConfirmTitle", "Delete Conversation?", "Xóa cuộc trò chuyện?"),
    ("Dialog_DeleteConfirmMessage", "Are you sure you want to delete this conversation? This action cannot be undone.", "Bạn có chắc chắn muốn xóa cuộc trò chuyện này? Hành động này không thể hoàn tác."),
    ("Dialog_LogoutConfirmTitle", "Log Out?", "Đăng xuất?"),
    ("Dialog_LogoutConfirmMessage", "Are you sure you want to log out of this account?", "Bạn có chắc chắn muốn đăng xuất khỏi tài khoản này?"),
    ("FilePicker_PhotoFilter", "Images (*.jpg;*.jpeg;*.png;*.webp;*.gif)", "Hình ảnh (*.jpg;*.jpeg;*.png;*.webp;*.gif)"),
    ("FilePicker_AllFilesFilter", "All Files (*.*)", "Tất cả tệp (*.*)"),
    ("FilePicker_SelectFileTitle", "Select File to Send", "Chọn tệp cần gửi"),
    ("FilePicker_SaveFileTitle", "Save File As", "Lưu tệp dưới dạng"),

    # Settings & Security
    ("Settings_Title", "Settings", "Cài đặt"),
    ("Settings_Appearance", "Appearance", "Giao diện"),
    ("Settings_Theme_Dark", "Dark", "Tối"),
    ("Settings_Theme_Light", "Light", "Sáng"),
    ("Settings_Theme_System", "System", "Hệ thống"),
    ("Settings_Language", "Language", "Ngôn ngữ"),
    ("Settings_Language_En", "English", "Tiếng Anh"),
    ("Settings_Language_Vi", "Tiếng Việt", "Tiếng Việt"),
    ("Settings_Security", "Security & Privacy", "Bảo mật & Quyền riêng tư"),
    ("Settings_SafetyNumber", "Safety Number", "Mã an toàn xác thực"),
    ("Settings_StorageCache", "Storage & Cache", "Lưu trữ & Dữ liệu đệm"),
    ("Settings_ClearCache", "Clear Cache", "Xóa dữ liệu đệm"),
    ("Settings_Logout", "Log Out", "Đăng xuất"),
]

RESX_HEADER = """<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
"""

def generate_resx(lang_index, out_path):
    lines = [RESX_HEADER]
    for key, en_val, vi_val in KEYS_DATA:
        val = en_val if lang_index == 0 else vi_val
        val_escaped = val.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace('"', "&quot;")
        lines.append(f'  <data name="{key}" xml:space="preserve">\n    <value>{val_escaped}</value>\n  </data>\n')
    lines.append("</root>\n")
    content = "".join(lines)
    # Ensure Windows CRLF
    content_crlf = content.replace("\r\n", "\n").replace("\n", "\r\n")
    # UTF-8 without BOM
    with open(out_path, "wb") as f:
        f.write(content_crlf.encode("utf-8"))
    print(f"Generated {out_path} ({len(KEYS_DATA)} keys, CRLF={content_crlf.count(chr(13)) == content_crlf.count(chr(10))})")

def generate_locale_keys(out_path):
    lines = [
        "// Copyright (c) Zenix. All rights reserved.\n",
        "// Licensed under the proprietary and confidential license.\n",
        "// Unauthorized copying, distribution, modification, or disclosure of this file,\n",
        "// via any medium, is strictly prohibited without prior written permission.\n",
        "\n",
        "namespace Zenix.Strings;\n",
        "\n",
        "/// <summary>\n",
        "/// Strongly-typed string identifier constants for localization resource lookup.\n",
        "/// </summary>\n",
        "public static class LocaleKeys\n",
        "{\n",
    ]
    current_category = ""
    for key, _, _ in KEYS_DATA:
        category = key.split("_")[0]
        if category != current_category:
            current_category = category
            lines.append(f"    // {category}\n")
        lines.append(f'    /// <summary>Resource key for {key}.</summary>\n')
        lines.append(f'    public const string {key} = "{key}";\n\n')
    lines.append("}\n")
    content = "".join(lines)
    content_crlf = content.replace("\r\n", "\n").replace("\n", "\r\n")
    with open(out_path, "wb") as f:
        f.write(content_crlf.encode("utf-8"))
    print(f"Generated {out_path} ({len(KEYS_DATA)} constants)")

if __name__ == "__main__":
    out_dir = "Zenix/Strings"
    os.makedirs(out_dir, exist_ok=True)
    generate_resx(0, os.path.join(out_dir, "String.resx"))
    generate_resx(0, os.path.join(out_dir, "String.en.resx"))
    generate_resx(1, os.path.join(out_dir, "String.vi.resx"))
    generate_locale_keys(os.path.join(out_dir, "LocaleKeys.cs"))
