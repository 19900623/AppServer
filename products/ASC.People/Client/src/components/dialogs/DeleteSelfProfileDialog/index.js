import React from "react";
import PropTypes from "prop-types";
import {
  toastr,
  ModalDialog,
  Button,
  Link,
  Text
} from "asc-web-components";
import { withTranslation } from "react-i18next";
import i18n from "./i18n";
import ModalDialogContainer from '../ModalDialogContainer';
import { api, utils } from "asc-web-common";
const { sendInstructionsToDelete } = api.people;
const { changeLanguage } = utils;

class DeleteSelfProfileDialogComponent extends React.Component {
  constructor(props) {
    super(props);

    this.state = {
      isRequestRunning: false
    };

    changeLanguage(i18n);
  }
  onDeleteSelfProfileInstructions = () => {
    const { onClose } = this.props;
    this.setState({ isRequestRunning: true }, () => {
      sendInstructionsToDelete()
        .then((res) => {
          toastr.success(res);
        })
        .catch((error) => toastr.error(error))
        .finally(() => {
          this.setState({ isRequestRunning: false }, () => onClose());
        });
    })
  }

  render() {
    console.log("DeleteSelfProfileDialog render");
    const { t, visible, email, onClose } = this.props;
    const { isRequestRunning } = this.state;

    return (
      <ModalDialogContainer>
        <ModalDialog
          visible={visible}
          onClose={onClose}
          headerContent={t('DeleteProfileTitle')}
          bodyContent={
            <Text fontSize='13px'>
              {t('DeleteProfileInfo')} <Link type="page" href={`mailto:${email}`} noHover color='#316DAA' title={email}>
                {email}
              </Link>
            </Text>
          }
          footerContent={
            <>
              <Button
                key="SendBtn"
                label={t('SendButton')}
                size="medium"
                primary={true}
                onClick={this.onDeleteSelfProfileInstructions}
                isLoading={isRequestRunning}
              />
              <Button
                className='button-dialog'
                key="CloseBtn"
                label={t('CloseButton')}
                size="medium"
                onClick={onClose}
                isDisabled={isRequestRunning}
              />
            </>
          }
        />
      </ModalDialogContainer>
    );
  }
}


const DeleteSelfProfileDialogTranslated = withTranslation()(DeleteSelfProfileDialogComponent);

const DeleteSelfProfileDialog = props => (
  <DeleteSelfProfileDialogTranslated i18n={i18n} {...props} />
);

DeleteSelfProfileDialog.propTypes = {
  visible: PropTypes.bool.isRequired,
  onClose: PropTypes.func.isRequired,
  email: PropTypes.string.isRequired,
};

export default DeleteSelfProfileDialog;
