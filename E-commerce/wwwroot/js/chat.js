/* AI Shopping Assistant Logic */

document.addEventListener('DOMContentLoaded', () => {
    // Inject HTML structure if not present (to make integration easier for user)
    if (!document.getElementById('ai-chat-widget')) {
        const widgetHTML = `
            <div id="ai-chat-widget">
                <button class="chat-widget-button" id="chatOpenBtn" aria-label="Open Chat">
                    <svg viewBox="0 0 24 24"><path d="M20 2H4c-1.1 0-2 .9-2 2v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm0 14H6l-2 2V4h16v12z"/></svg>
                </button>

                <div class="chat-container" id="chatContainer">
                    <div class="chat-header">
                        <h3>AI Assistant</h3>
                        <button class="chat-close" id="chatCloseBtn">&times;</button>
                    </div>
                    <div class="chat-messages" id="chatMessages">
                        <div class="message bot">
                            Hello! I'm your AI shopping assistant. How can I help you find the perfect music today?
                        </div>
                    </div>
                    <div class="chat-input-area">
                        <input type="text" class="chat-input" id="chatInput" placeholder="Ask about products..." />
                        <button class="chat-send" id="chatSendBtn" aria-label="Send">
                            <svg viewBox="0 0 24 24"><path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/></svg>
                        </button>
                    </div>
                </div>
            </div>
        `;
        document.body.insertAdjacentHTML('beforeend', widgetHTML);
    }

    // Elements
    const openBtn = document.getElementById('chatOpenBtn');
    const closeBtn = document.getElementById('chatCloseBtn');
    const container = document.getElementById('chatContainer');
    const messages = document.getElementById('chatMessages');
    const input = document.getElementById('chatInput');
    const sendBtn = document.getElementById('chatSendBtn');

    // State
    let isOpen = false;

    // Toggles
    function toggleChat() {
        isOpen = !isOpen;
        if (isOpen) {
            container.classList.add('open');
            input.focus();
        } else {
            container.classList.remove('open');
        }
    }

    openBtn.addEventListener('click', toggleChat);
    closeBtn.addEventListener('click', toggleChat);

    // Messaging
    async function sendMessage() {
        const text = input.value.trim();
        if (!text) return;

        // User Message
        appendMessage(text, 'user');
        input.value = '';

        // Loading Indicator
        const loadingId = appendLoading();

        try {
            // NOTE: This URL assumes the backend Razor Page is mapped to /Chat
            // If the user puts it elsewhere, they might need to adjust this.
            // Using 'fetch' to POST json data
            const response = await fetch('/Chat?handler=Ask', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    // Tries to get anti-forgery token if present in standard form (might fail if not present, but handling gracefully)
                    'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value || ''
                },
                body: JSON.stringify({ message: text })
            });

            removeLoading(loadingId);

            if (response.ok) {
                const data = await response.json();
                appendMessage(data.answer || "I'm sorry, I couldn't get a response.", 'bot');
            } else {
                console.error("Server Error:", response.statusText);
                appendMessage("Sorry, I'm having trouble connecting to the server. Please check the backend configuration.", 'bot');
            }

        } catch (error) {
            removeLoading(loadingId);
            console.error("Network Error:", error);
            appendMessage("Network error. Please try again later.", 'bot');
        }
    }

    sendBtn.addEventListener('click', sendMessage);
    input.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') sendMessage();
    });

    // Helpers
    function appendMessage(text, sender) {
        const div = document.createElement('div');
        div.classList.add('message', sender);
        div.textContent = text;
        messages.appendChild(div);
        scrollToBottom();
    }

    function appendLoading() {
        const id = 'loading-' + Date.now();
        const div = document.createElement('div');
        div.id = id;
        div.classList.add('typing-indicator');
        div.innerHTML = `<div class="typing-dot"></div><div class="typing-dot"></div><div class="typing-dot"></div>`;
        messages.appendChild(div);
        scrollToBottom();
        return id;
    }

    function removeLoading(id) {
        const el = document.getElementById(id);
        if (el) el.remove();
    }

    function scrollToBottom() {
        messages.scrollTop = messages.scrollHeight;
    }
});
