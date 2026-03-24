window.helpers = window.helpers || {};

window.helpers.openFileDialog = (id) => {
    document.getElementById(id)?.click();
};
window.helpers = window.helpers || {};

window.helpers.registerDragEvents = (container, dotnet) => {
    if (!container || !dotnet) {
        console.warn("Drag events registered without container or dotnet reference");
        return;
    }

    let dragCounter = 0;

    container.addEventListener("dragenter", e => {
        e.preventDefault();
        dragCounter++;
        dotnet.invokeMethodAsync("ShowOverlay");
    });

    container.addEventListener("dragleave", e => {
        e.preventDefault();
        dragCounter--;
        if (dragCounter === 0) dotnet.invokeMethodAsync("HideOverlay");
    });

    container.addEventListener("dragover", e => e.preventDefault());

    container.addEventListener("drop", e => {
        e.preventDefault();
        dragCounter = 0;
        dotnet.invokeMethodAsync("HideOverlay");

        const inputEl = document.getElementById("fileInput");
        if (!inputEl) return;

        const dt = new DataTransfer();
        for (let i = 0; i < e.dataTransfer.files.length; i++) {
            dt.items.add(e.dataTransfer.files[i]);
        }

        inputEl.files = dt.files;
        inputEl.dispatchEvent(new Event("change", { bubbles: true }));
    });
};

window.helpers.setEnabled = function (value) {
    window.onbeforeunload = value ? function (e) {
        e.preventDefault();
        e.returnValue = '';
    } : null;
};
window.helpers.startFileDownload = (fileName) => {
    window._fileBuffers = window._fileBuffers || {};
    window._fileBuffers[fileName] = [];
};

window.helpers.writeFileChunk = async (fileName, dotnetStreamRef) => {
    const readableStream = await dotnetStreamRef.stream();
    const reader = readableStream.getReader();

    while (true) {
        const { done, value } = await reader.read();
        if (done) break;
        window._fileBuffers[fileName].push(value);
    }
};

window.helpers.finishFileDownload = (fileName) => {
    const allChunks = window._fileBuffers[fileName];
    if (!allChunks) return;

    const blob = new Blob(allChunks, { type: "application/octet-stream" });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(link.href);

    delete window._fileBuffers[fileName];
};
window.helpers.cleanupFileChunks = (fileName) => {
    if (window._fileBuffers && window._fileBuffers[fileName]) {
        delete window._fileBuffers[fileName];
    }
};

window.helpers.getFileBytesFromChunks = async (fileName) => {
    const chunks = window._fileBuffers[fileName];
    const blob = new Blob(chunks);
    const buffer = await blob.arrayBuffer();
    return new Uint8Array(buffer);
};

window.helpers.selectInputText = (element) => {
    element.select();
}

window.helpers.encryptPasswordWithPublicKey = async (password, base64Key) => {
    const keyBytes = Uint8Array.from(atob(base64Key), c => c.charCodeAt(0));
    const key = await crypto.subtle.importKey(
        "spki",
        keyBytes.buffer,
        {
            name: "RSA-OAEP",
            hash: "SHA-256"
        },
        true,
        ["encrypt"]
    );

    const enc = new TextEncoder();
    const encrypted = await crypto.subtle.encrypt(
        { name: "RSA-OAEP" },
        key,
        enc.encode(password)
    );

    // Convert encrypted ArrayBuffer to Base64
    let binary = '';
    let bytes = new Uint8Array(encrypted);
    bytes.forEach(b => binary += String.fromCharCode(b));
    return btoa(binary);
}