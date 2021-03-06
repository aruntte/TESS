import { MainRc } from "./Main.js";
import {
    CtrlAltDelDto,
    KeyDownDto,
    KeyPressDto,
    KeyUpDto,
    MouseDownDto,
    MouseMoveDto,
    MouseUpDto,
    MouseWheelDto,
    QualityChangeDto,
    SelectScreenDto,
    TapDto,
    AutoQualityAdjustDto,
    ToggleAudioDto,
    ToggleBlockInputDto,
    ClipboardTransferDto,
    FileDto,
    WindowsSessionsDto,
    GenericDto
} from "./RtcDtos.js";
import { CreateGUID, When } from "../Utilities.js";
import { FileTransferProgress } from "./UI.js";
import { BinaryDtoType } from "../Enums/BinaryDtoType.js";

export class MessageSender {
    GetWindowsSessions() {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new WindowsSessionsDto()),
            () => MainRc.RCHubConnection.GetWindowsSessions());
    }
    ChangeWindowsSession(sessionId: number) {
        MainRc.RCHubConnection.ChangeWindowsSession(sessionId);
    }
    SendSelectScreen(displayName: string) {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new SelectScreenDto(displayName)),
            () => MainRc.RCHubConnection.SendSelectScreen(displayName));
    }
    SendMouseMove(percentX: number, percentY: number) {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new MouseMoveDto(percentX, percentY)),
            () => MainRc.RCHubConnection.SendMouseMove(percentX, percentY));
    }
    SendMouseDown(button: number, percentX: number, percentY: number) {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new MouseDownDto(button, percentX, percentY)),
            () => MainRc.RCHubConnection.SendMouseDown(button, percentX, percentY));
    }
    SendMouseUp(button: number, percentX: number, percentY: number) {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new MouseUpDto(button, percentX, percentY)),
            () => MainRc.RCHubConnection.SendMouseUp(button, percentX, percentY));
    }
    SendTap(percentX: number, percentY: number) {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new TapDto(percentX, percentY)),
            () => MainRc.RCHubConnection.SendTap(percentX, percentY));
    }
    SendMouseWheel(deltaX: number, deltaY: number) {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new MouseWheelDto(deltaX, deltaY)),
            () => MainRc.RCHubConnection.SendMouseWheel(deltaX, deltaY));
    }
    SendKeyDown(key: string) {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new KeyDownDto(key)),
            () => MainRc.RCHubConnection.SendKeyDown(key));
    }
    SendKeyUp(key: string) {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new KeyUpDto(key)),
            () => MainRc.RCHubConnection.SendKeyUp(key));
    }
    SendKeyPress(key: string) {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new KeyPressDto(key)),
            () => MainRc.RCHubConnection.SendKeyPress(key));
    }
    SendSetKeyStatesUp() {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new GenericDto(BinaryDtoType.SetKeyStatesUp)),
            () => MainRc.RCHubConnection.SendSetKeyStatesUp());
    }
    SendCtrlAltDel() {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new CtrlAltDelDto()),
            () => MainRc.RCHubConnection.SendCtrlAltDel());
    }

    async SendFile(buffer: Uint8Array, fileName: string) {
        var messageId = CreateGUID();

        this.SendToAgent(() => MainRc.RtcSession.SendDto(new FileDto(null, fileName, messageId, false, true)),
            () => MainRc.RCHubConnection.SendFile(null, fileName, messageId, false, true));

        for (var i = 0; i < buffer.byteLength; i += 50_000) {

            await this.SendToAgentAsync(async () => {
                MainRc.RtcSession.SendDto(new FileDto(buffer.slice(i, i + 50_000), fileName, messageId, false, false));
                await When(() => MainRc.RtcSession.DataChannel.bufferedAmount == 0, 10);
            }, async () => {
                    await MainRc.RCHubConnection.SendFile(buffer.slice(i, i + 50_000), fileName, messageId, false, false);
            });

            if (i > 0) {
                FileTransferProgress.value = i / buffer.byteLength;
            }
        }

        this.SendToAgent(() => MainRc.RtcSession.SendDto(new FileDto(null, fileName, messageId, true, false)),
            () => MainRc.RCHubConnection.SendFile(null, fileName, messageId, true, false));
    }

    SendQualityChange(qualityLevel: number) {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new QualityChangeDto(qualityLevel)),
            () => MainRc.RCHubConnection.SendQualityChange(qualityLevel));
    }
    SendAutoQualityAdjust(isOn: boolean) {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new AutoQualityAdjustDto(isOn)),
            () => MainRc.RCHubConnection.SendAutoQualityAdjust(isOn));
    }
    SendToggleAudio(toggleOn: boolean) {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new ToggleAudioDto(toggleOn)),
            () => MainRc.RCHubConnection.SendToggleAudio(toggleOn));
    };
    SendToggleBlockInput(toggleOn: boolean) {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new ToggleBlockInputDto(toggleOn)),
            () => MainRc.RCHubConnection.SendToggleBlockInput(toggleOn));
    }
    SendClipboardTransfer(text: string, typeText: boolean) {
        this.SendToAgent(() => MainRc.RtcSession.SendDto(new ClipboardTransferDto(text, typeText)),
            () => MainRc.RCHubConnection.SendClipboardTransfer(text, typeText));
    }

    private IsWebRtcAvailable() {
        return MainRc.RtcSession.DataChannel && MainRc.RtcSession.DataChannel.readyState == "open";
    }

    private SendToAgent(rtcSend: () => void, websocketSend: () => void) {
        if (MainRc.RtcSession.DataChannel && MainRc.RtcSession.DataChannel.readyState == "open") {
            rtcSend();
        }
        else if (MainRc.RCHubConnection.Connection.connectionStarted) {
            websocketSend();
        }
    }

    private async SendToAgentAsync(rtcSend: () => Promise<any>, websocketSend: () => Promise<any>) {
        if (this.IsWebRtcAvailable()) {
            await rtcSend();
        }
        else {
            await websocketSend();
        }
    }

  
}