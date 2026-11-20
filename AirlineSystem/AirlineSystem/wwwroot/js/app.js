// Initiate GET request (AJAX-supported)
$(document).on('click', '[data-get]', e => {
    e.preventDefault();
    const url = e.target.dataset.get;
    location = url || location;
});

// Initiate POST request (AJAX-supported)
$(document).on('click', '[data-post]', e => {
    e.preventDefault();
    const url = e.target.dataset.post;
    const f = $('<form>').appendTo(document.body)[0];
    f.method = 'post';
    f.action = url || location;
    f.submit();
});

// Trim input
$('[data-trim]').on('change', e => {
    e.target.value = e.target.value.trim();
});

// Auto uppercase
$('[data-upper]').on('input', e => {
    const a = e.target.selectionStart;
    const b = e.target.selectionEnd;
    e.target.value = e.target.value.toUpperCase();
    e.target.setSelectionRange(a, b);
});

// RESET form
$('[type=reset]').on('click', e => {
    e.preventDefault();
    location = location;
});

// Check all checkboxes
$('[data-check]').on('click', e => {
    e.preventDefault();
    const name = e.target.dataset.check;
    $(`[name=${name}]`).prop('checked', true);
});

// Uncheck all checkboxes
$('[data-uncheck]').on('click', e => {
    e.preventDefault();
    const name = e.target.dataset.uncheck;
    $(`[name=${name}]`).prop('checked', false);
});

// Row checkable (AJAX-supported)
$(document).on('click', '[data-checkable]', e => {
    if ($(e.target).is(':input,a')) return;
    
    $(e.currentTarget)
        .find(':checkbox')
        .prop('checked', (i, v) => !v);
});

// Photo preview
$('.upload input').on('change', e => {
    const f = e.target.files[0];
    const img = $(e.target).siblings('img')[0];

    img.dataset.src ??= img.src;

    if (f && f.type.startsWith('image/')) {
        img.onload = e => URL.revokeObjectURL(img.src);
        img.src = URL.createObjectURL(f);
    }
    else {
        img.src = img.dataset.src;
        e.target.value = '';
    }

    // Trigger input validation
    $(e.target).valid();
});

$('.upload')
    .on('dragenter dragover', function (e) {
        e.preventDefault();
        e.stopPropagation();
        $(this).addClass('dragover');
    })
    .on('dragleave drop', function (e) {
        e.preventDefault();
        e.stopPropagation();
        $(this).removeClass('dragover');
    })
    .on('drop', function (e) {
        const files = e.originalEvent.dataTransfer?.files;
        if (files && files.length > 0) {
            const input = $(this).find('input[type=file]')[0];

            //Assigned dropped file to the hidden file input using DataTransfer API
            const dt = new DataTransfer();
            dt.items.add(files[0]);
            input.files = dt.files;

            // Trigger existing change event (handles preview & validation automatically)
            $(input).trigger('change');
        }
    });

// -------------------------------------------------------
//            W#ebcam Photo Capture
// -------------------------------------------------------
let cameraStream = null;
let targetFileInput = null;

$(document).on('click', '[data-camera]', async function (e) {
    e.preventDefault();

    // Find the file input in the same form or upload container
    targetFileInput = $(this).closest('form, div').find('.upload input[type=file]')[0] || $('input[type=file]')[0];

    // Create the modal dialog dynamically if it doesn't exist yet
    let dialog = document.getElementById('camera-dialog');
    if (!dialog) {
        $('body').append(`
            <dialog id="camera-dialog" style="padding: 20px; border-radius: 8px; border: 1px solid #999; text-align: center;">
                <h3 style="margin-top: 0;">Take a Photo</h3>
                <video id="camera-video" width="280" height="280" autoplay playsinline class="camera-video"></video>
                <canvas id="camera-canvas" width="280" height="280" style="display: none;"></canvas>
                <div style="margin-top: 15px; display: flex; gap: 10px; justify-content: center;">
                    <button type="button" id="camera-snap">Capture</button>
                    <button type="button" id="camera-cancel">Cancel</button>
                </div>
            </dialog>
        `);
        dialog = document.getElementById('camera-dialog');

        // Handle Capture button click
        $(document).on('click', '#camera-snap', function () {
            const video = document.getElementById('camera-video');
            const canvas = document.getElementById('camera-canvas');
            const ctx = canvas.getContext('2d');
            ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
            canvas.toBlob(blob => {
                if (blob && targetFileInput) {
                    const file = new File([blob], 'webcam.jpg', { type: 'image/jpeg' });
                    const dt = new DataTransfer();
                    dt.items.add(file);
                    targetFileInput.files = dt.files;

                    // Trigger change to update the image preview & validation automatically
                    $(targetFileInput).trigger('change');
                }
                closeCamera();
            }, 'image/jpeg', 0.95);
        });

        // Handle Cancel button click
        $(document).on('click', '#camera-cancel', function () {
            closeCamera();
        });
    }
    // Start video stream 
    try {
        cameraStream = await navigator.mediaDevices.getUserMedia({
            video: { width: 300, height: 300, facingMode: 'user' },
            audio: false
        });
        document.getElementById('camera-video').srcObject = cameraStream;
        dialog.showModal();

    } catch (err) {
        alert('Cannot access webcam. Please allow camera permissions in your browser.');
    }
});

function closeCamera() {
    if (cameraStream) {
        cameraStream.getTracks().forEach(track => track.stop());
        cameraStream = null;
    }
    const dialog = document.getElementById('camera-dialog');
    if (dialog && dialog.open) {
        dialog.close();
    }
}