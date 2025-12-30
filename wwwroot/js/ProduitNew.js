// wwwroot/js/ProduitNew.js

document.addEventListener("DOMContentLoaded", function () {
    var fileInput = document.getElementById("ProductImage");
    var fileNameSpan = document.getElementById("ProductImageName");

    if (fileInput && fileNameSpan) {
        fileInput.addEventListener("change", function () {
            if (this.files && this.files.length > 0) {
                fileNameSpan.textContent = this.files[0].name;
            } else {
                fileNameSpan.textContent = "";
            }
        });
    }
});
