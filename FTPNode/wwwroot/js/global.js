document.addEventListener('mousedown', function (e) {
    if (e.shiftKey && e.target.closest('#filesContainer')) {
        e.preventDefault();
    }
});