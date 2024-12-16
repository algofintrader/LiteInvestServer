function scrollToRow(gridItemId, rowIndex) {
    let parent = document.getElementById(gridItemId);
    if (parent) {
        let scrollableElement = parent.querySelector(".k-grid-content");
        if (scrollableElement) {
            let rows = scrollableElement.querySelectorAll(".k-table-tbody tr");

            if (rows.length <= rowIndex) {
                let rowHeight = rows.length > 0 ? rows[0].offsetHeight : 30;
                let targetScrollTop = rowIndex * rowHeight;

                scrollableElement.scrollTop = targetScrollTop;

                setTimeout(function () {
                    let rowsAfterScroll = scrollableElement.querySelectorAll(".k-table-tbody tr");
                    if (rowsAfterScroll.length > rowIndex) {
                        let targetRow = rowsAfterScroll[rowIndex];
                        targetRow.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    }
                }, 300);
            } else {
                let targetRow = rows[rowIndex];
                targetRow.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        }
    }
}
