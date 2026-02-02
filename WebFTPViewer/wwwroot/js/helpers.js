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