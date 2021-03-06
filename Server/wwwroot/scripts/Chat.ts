import * as HubConnection from "./HubConnection.js";

export function CreateChatWindow(deviceID: string, deviceName: string) {
    var chatWindow = document.getElementById("chat-" + deviceID);
    if (!chatWindow) {
        var windowHtml = `
            <div class="chat-header">
                <div class="text-right">
                    <i class="fas fa-window-close close-button pointer"></i>
                </div>
                <h6>Chat with ${deviceName}</h6>
            </div>
            <div class="chat-messages">
            </div>
            <textarea class="chat-input" value=""></textarea>
        `;
        chatWindow = document.createElement("div");
        chatWindow.classList.add("chat-window");
        chatWindow.style.right = "20px";
        chatWindow.style.bottom = "20px";
        chatWindow.setAttribute("id", "chat-" + deviceID);
        chatWindow.innerHTML = windowHtml;
        document.body.appendChild(chatWindow);

        chatWindow.addEventListener("mousedown", (ev) => {
            document.querySelectorAll(".chat-window").forEach(x => {
                (x as HTMLDivElement).style.zIndex = "0";
            });
            (ev.currentTarget as HTMLDivElement).style.zIndex = "1";
        });

        (chatWindow.querySelector(".close-button") as HTMLElement).onclick = (ev) => {
            ev.preventDefault();
            ev.stopPropagation();
            document.body.removeChild(chatWindow);
        };

        (chatWindow.querySelector(".chat-header") as HTMLDivElement).onmousedown = (ev) => {
            ev.preventDefault();
            chatWindow.removeEventListener("mousemove", moveChatWindow);
            chatWindow.removeEventListener("mouseup", stopMovingChatWindow);
            chatWindow.removeEventListener("mouseleave", stopMovingChatWindow);
            chatWindow.addEventListener("mousemove", moveChatWindow);
            chatWindow.addEventListener("mouseup", stopMovingChatWindow);
            chatWindow.addEventListener("mouseleave", stopMovingChatWindow);
        };

        (chatWindow.querySelector(".chat-input") as HTMLTextAreaElement).onkeypress = (ev) => {
            if (ev.key.toLowerCase() == "enter") {
                ev.preventDefault();
                ev.stopPropagation();
                var inputText = (ev.currentTarget as HTMLTextAreaElement).value;
                (chatWindow.querySelector(".chat-messages") as HTMLDivElement).innerHTML += `
                    <div>
                        <span class="text-primary">You: </span>
                        <span>${inputText}</span>
                    </div>
                `;
                (ev.currentTarget as HTMLTextAreaElement).value = "";
                HubConnection.Connection.invoke("Chat", inputText, [deviceID]);
                var chatMessages = chatWindow.querySelector(".chat-messages") as HTMLDivElement;
                chatMessages.scrollTo({ top: chatMessages.scrollHeight });
            }
        };
    }
}

export function ReceiveChatText(deviceID: string, deviceName: string, message: string) {
    CreateChatWindow(deviceID, deviceName);
    var chatWindow = document.getElementById("chat-" + deviceID) as HTMLDivElement;
    var chatMessages = chatWindow.querySelector(".chat-messages") as HTMLDivElement;
    chatMessages.innerHTML += `
        <div>
            <span class="text-primary">${deviceName}: </span>
            <span>${message}</span>
        </div>
    `;
    chatMessages.scrollTo({ top: chatMessages.scrollHeight });
}

function moveChatWindow(ev: MouseEvent) {
    var chatWindow = ev.currentTarget as HTMLDivElement;
    chatWindow.style.right = String(parseInt(chatWindow.style.right || "0") - ev.movementX) + "px";
    chatWindow.style.bottom = String(parseInt(chatWindow.style.bottom || "0") - ev.movementY) + "px";
}

function stopMovingChatWindow(ev: MouseEvent) {
    ev.currentTarget.removeEventListener("mousemove", moveChatWindow);
    ev.currentTarget.removeEventListener("mouseup", stopMovingChatWindow);
    ev.currentTarget.removeEventListener("mouseleave", stopMovingChatWindow);
}