import styled from 'styled-components';

const ModalDialogContainer = styled.div`

.flex {
  display: flex;
  justify-content: space-between;
  }

  .text-dialog {
    margin: 16px 0;
  }

  .input-dialog {
    margin-top: 16px;
  }

  .button-dialog {
    margin-left: 8px;
  }

  .warning-text {
    margin: 20px 0; 
  }

  .textarea-dialog {
    margin-top: 12px;
  }

  .link-dialog {
    margin-right: 12px;
  }

  .error-label {
    position: absolute;
    max-width: 100%;
  }

  .field-body {
    position: relative;
  }

  .toggle-content-dialog {
    .heading-toggle-content {
      font-size: 16px;
    }
  }

  .send_invite_dialog-content {
    max-height: 290px;
    overflow-y: scroll;
    border: 1px solid lightgray;
    padding: 8px 0px 0px 8px;
  }
`;

export default ModalDialogContainer;