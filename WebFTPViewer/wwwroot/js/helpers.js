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

        // optionally forward to InputFile here
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
window.helpers.streamFileDownload = (fileName) => {
    const stream = new WritableStream({
        write(chunk) {
            // append each chunk to the internal buffer
            if (!window._fileBuffers) window._fileBuffers = {};
            if (!window._fileBuffers[fileName]) {
                window._fileBuffers[fileName] = [];
            }
            window._fileBuffers[fileName].push(chunk);
        },
        close() {
            const allChunks = window._fileBuffers[fileName];
            const blob = new Blob(allChunks, { type: "application/octet-stream" });
            const link = document.createElement('a');
            link.href = URL.createObjectURL(blob);
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(link.href);
            delete window._fileBuffers[fileName];
        }
    });

    return stream;
};

window.helpers.writeFileChunk = (fileName, base64Chunk) => {
    const uint8Array = Uint8Array.from(atob(base64Chunk), c => c.charCodeAt(0));
    if (window._fileBuffers && window._fileBuffers[fileName]) {
        window._fileBuffers[fileName].push(uint8Array);
    } else {
        window._fileBuffers = window._fileBuffers || {};
        window._fileBuffers[fileName] = [uint8Array];
    }
};
