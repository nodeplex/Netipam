window.netipamDragOrder = {
    init: function (container, dotnetRef, listKey) {
        if (!container) return;

        var lastOver = null;

        container.addEventListener("dragstart", function (e) {
            var handle = e.target.closest(".drag-handle");
            if (!handle || !e.dataTransfer) return;

            var dragId = handle.getAttribute("data-drag-id");
            if (!dragId) return;

            e.dataTransfer.setData("text/plain", dragId);
            e.dataTransfer.effectAllowed = "move";
            e.dataTransfer.dropEffect = "move";
            container.dataset.dragId = dragId;
        });

        container.addEventListener("dragover", function (e) {
            e.preventDefault();
            var row = e.target.closest(".drag-row");
            if (!row || row === lastOver) return;

            if (lastOver)
                lastOver.classList.remove("drag-over");

            row.classList.add("drag-over");
            lastOver = row;
        });

        container.addEventListener("drop", function (e) {
            e.preventDefault();

            var target = e.target.closest("[data-drag-index]");
            if (!target) return;

            var dragId = container.dataset.dragId || (e.dataTransfer ? e.dataTransfer.getData("text/plain") : "");
            if (dragId === "") return;

            var toIndex = target.getAttribute("data-drag-index");
            if (toIndex === null) return;

            if (lastOver)
                lastOver.classList.remove("drag-over");
            lastOver = null;
            dotnetRef.invokeMethodAsync("HandleDrop", listKey, parseInt(dragId, 10), parseInt(toIndex, 10));
        });

        container.addEventListener("dragend", function () {
            if (lastOver)
                lastOver.classList.remove("drag-over");
            lastOver = null;
        });
    }
};
