import React from 'react'
import styled from 'styled-components'
import PropTypes from 'prop-types'
import isEqual from "lodash/isEqual";

import { tablet } from '../../utils/device';
import InputBlock from '../input-block'
import { Icons } from '../icons'
import Link from '../link'
import { Text } from '../text'
//import DropDown from '../drop-down'

import Tooltip from "../tooltip";

// eslint-disable-next-line no-unused-vars
const SimpleInput = ({ onValidateInput, onCopyToClipboard, ...props }) => <div {...props}></div>;

SimpleInput.propTypes = {
  onValidateInput: PropTypes.func,
  onCopyToClipboard: PropTypes.func
}

const StyledInput = styled(SimpleInput)`
  display: flex;
  align-items: center;
  line-height: 32px;
  flex-direction: row;
  flex-wrap: nowrap;

  @media ${tablet} {
    flex-wrap: wrap;
  } 
`;

const PasswordProgress = styled.div`
  ${props => props.inputWidth ? `width: ${props.inputWidth};` : `flex: auto;`}
`;

const NewPasswordButton = styled.div`
  margin-left: 16px;
  margin-top: -6px;
`;

const CopyLink = styled.div`
  margin-top: -6px;
  margin-left: 16px;

  @media ${tablet} {
    width: 100%;
    margin-left: 0px;
    margin-top: 8px;
  }
`;

const TooltipStyle = styled.div`
  .__react_component_tooltip {
    
  }
`;
const Progress = styled.div`
  border: 3px solid ${props => (!props.isDisabled && props.progressColor) ? props.progressColor : 'transparent'};
  border-radius: 2px;
  margin-top: -4px;
  width: ${props => props.progressWidth ? props.progressWidth + '%' : '0%'};
`;

const StyledTooltipContainer = styled(Text.Body)`
  //margin: 8px 16px 16px 16px;
`;

const StyledTooltipItem = styled(Text.Body)`
  margin-left: 8px;
  height: 24px;
  color: ${props => props.valid ? '#44bb00' : '#B40404'};
`;

class PasswordInput extends React.Component {

  constructor(props) {
    super(props);

    const { inputValue, inputType } = props;

    this.ref = React.createRef();
    this.refTooltip = React.createRef();

    this.state = {
      type: inputType,
      progressColor: 'transparent',
      progressWidth: 0,
      inputValue: inputValue,
      displayTooltip: false,
      validLength: false,
      validDigits: false,
      validCapital: false,
      validSpecial: false
    }
  }

  /*onFocus = () => {
    this.setState({
      displayTooltip: true
    });
  }*/

  onBlur = () => {
    /*this.setState({
      displayTooltip: false
    });*/
    this.refTooltip.current.hideTooltip();
  }

  changeInputType = () => {
    const newType = this.state.type === 'text' ? 'password' : 'text';

    this.setState({
      type: newType
    });
  }

  testStrength = value => {
    const { generatorSpecial, passwordSettings } = this.props;
    const specSymbols = new RegExp('[' + generatorSpecial + ']');

    let capital;
    let digits;
    let special;

    passwordSettings.upperCase
      ? capital = /[A-Z]/.test(value)
      : capital = true;

    passwordSettings.digits
      ? digits = /\d/.test(value)
      : digits = true;

    passwordSettings.specSymbols
      ? special = specSymbols.test(value)
      : special = true;

    return {
      digits: digits,
      capital: capital,
      special: special,
      length: value.trim().length >= passwordSettings.minLength
    };
  }

  checkPassword = (value) => {
    const greenColor = '#44bb00';
    const redColor = '#B40404';
    const passwordValidation = this.testStrength(value);
    const progressScore = passwordValidation.digits
      && passwordValidation.capital
      && passwordValidation.special
      && passwordValidation.length;
    const progressWidth = value.trim().length * 100 / this.props.passwordSettings.minLength;
    const progressColor = progressScore
      ? greenColor
      : (value.length === 0)
        ? 'transparent'
        : redColor;

    this.props.onValidateInput && this.props.onValidateInput(progressScore);

    this.setState({
      progressColor: progressColor,
      progressWidth: progressWidth > 100 ? 100 : progressWidth,
      inputValue: value,
      validLength: passwordValidation.length,
      validDigits: passwordValidation.digits,
      validCapital: passwordValidation.capital,
      validSpecial: passwordValidation.special
    });
  }

  onChangeAction = (e) => {
    this.props.onChange && this.props.onChange(e);
    this.checkPassword(e.target.value);
  }

  onGeneratePassword = (e) => {
    if (this.props.isDisabled)
      return e.preventDefault();

    const newPassword = this.getNewPassword();
    
    if (this.state.type !== 'text') {
      this.setState({
        type: 'text'
      });
    }
    
    this.checkPassword(newPassword);
    this.props.onChange && this.props.onChange({ target: { value: newPassword } });
  }

  getNewPassword = () => {
    const { passwordSettings, generatorSpecial } = this.props;

    const length = passwordSettings.minLength;
    const string = 'abcdefghijklmnopqrstuvwxyz';
    const numeric = '0123456789';
    const special = generatorSpecial;

    let password = '';
    let character = '';

    while (password.length < length) {
      const a = Math.ceil(string.length * Math.random() * Math.random());
      const b = Math.ceil(numeric.length * Math.random() * Math.random());
      const c = Math.ceil(special.length * Math.random() * Math.random());

      let hold = string.charAt(a);

      if (passwordSettings.upperCase) {
        hold = (password.length % 2 == 0)
          ? (hold.toUpperCase())
          : (hold);
      }

      character += hold;

      if (passwordSettings.digits) {
        character += numeric.charAt(b);
      }

      if (passwordSettings.specSymbols) {
        character += special.charAt(c);
      }

      password = character;
    }

    password = password
      .split('')
      .sort(() => 0.5 - Math.random())
      .join('');

    return password.substr(0, length);
  }

  copyToClipboard = emailInputName => {
    const { clipEmailResource, clipPasswordResource, isDisabled, onCopyToClipboard } = this.props;

    if (isDisabled)
      return event.preventDefault();

    const textField = document.createElement('textarea');
    const emailValue = document.getElementsByName(emailInputName)[0].value;
    const formattedText = clipEmailResource + emailValue + ' | ' + clipPasswordResource + this.state.inputValue;

    textField.innerText = formattedText;
    document.body.appendChild(textField);
    textField.select();
    document.execCommand('copy');
    textField.remove();

    onCopyToClipboard && onCopyToClipboard(formattedText);
  }

  shouldComponentUpdate(nextProps, nextState) {
    return !isEqual(this.props, nextProps) || !isEqual(this.state, nextState);
  }

  render() {
    //console.log('PasswordInput render()');
    const {
      inputName,
      isDisabled,
      scale,
      size,
      clipActionResource,
      tooltipPasswordTitle,
      tooltipPasswordLength,
      tooltipPasswordDigits,
      tooltipPasswordCapital,
      tooltipPasswordSpecial,
      emailInputName,
      inputWidth,
      passwordSettings,
      hasError,
      hasWarning,
      placeholder,
      tabIndex,
      maxLength,
      onValidateInput,
      id,
      autoComplete,
      className,
      tooltipOffsetLeft
    } = this.props;
    const {
      type,
      progressColor,
      progressWidth,
      inputValue,
      validLength,
      validDigits,
      validCapital,
      validSpecial,
      //displayTooltip
    } = this.state;

    const iconsColor = isDisabled ? '#D0D5DA' : '#A3A9AE';
    const iconName = type === 'password' ? 'EyeIcon' : 'EyeOffIcon';

    const tooltipContent = (
      <StyledTooltipContainer forwardedAs='div' title={tooltipPasswordTitle}>
        {tooltipPasswordTitle}
        <StyledTooltipItem forwardedAs='div' title={tooltipPasswordLength} valid={validLength} >
          {tooltipPasswordLength}
        </StyledTooltipItem>
        {passwordSettings.digits &&
          <StyledTooltipItem forwardedAs='div' title={tooltipPasswordDigits} valid={validDigits} >
            {tooltipPasswordDigits}
          </StyledTooltipItem>
        }
        {passwordSettings.upperCase &&
          <StyledTooltipItem forwardedAs='div' title={tooltipPasswordCapital} valid={validCapital} >
            {tooltipPasswordCapital}
          </StyledTooltipItem>
        }
        {passwordSettings.specSymbols &&
          <StyledTooltipItem forwardedAs='div' title={tooltipPasswordSpecial} valid={validSpecial} >
            {tooltipPasswordSpecial}
          </StyledTooltipItem>
        }
      </StyledTooltipContainer>
    );

    return (
      <StyledInput onValidateInput={onValidateInput} className={className}>
        <PasswordProgress
          inputWidth={inputWidth}
          data-for="tooltipContent"
          data-tip=""
          data-event="click"
          ref={this.ref}
        >
          <InputBlock
            id={id}
            name={inputName}
            hasError={hasError}
            isDisabled={isDisabled}
            iconName={iconName}
            value={inputValue}
            onIconClick={this.changeInputType}
            onChange={this.onChangeAction}
            scale={scale}
            size={size}
            type={type}
            iconColor={iconsColor}
            isIconFill={true}
            //onFocus={this.onFocus}
            onBlur={this.onBlur}
            hasWarning={hasWarning}
            placeholder={placeholder}
            tabIndex={tabIndex}
            maxLength={maxLength}
            autoComplete={autoComplete}
          >
          </InputBlock>
          <TooltipStyle>
            <Tooltip
              id="tooltipContent"
              effect="solid"
              place="top"
              offsetLeft={tooltipOffsetLeft}
              reference={this.refTooltip}
            >
              {tooltipContent}
            </Tooltip>
          </TooltipStyle>
          <Progress progressColor={progressColor} progressWidth={progressWidth} isDisabled={isDisabled} />
        </PasswordProgress>
        <NewPasswordButton>
          <Icons.RefreshIcon
            size="medium"
            color={iconsColor}
            isfill={true}
            onClick={this.onGeneratePassword}
          />
        </NewPasswordButton>
        <CopyLink>
          <Link
            type="action"
            isHovered={true}
            fontSize={13}
            color={iconsColor}
            onClick={this.copyToClipboard.bind(this, emailInputName)}
          >
            {clipActionResource}
          </Link>
        </CopyLink>
      </StyledInput>
    );
  }
}

PasswordInput.propTypes = {
  inputType: PropTypes.oneOf(['text', 'password']),
  inputName: PropTypes.string,
  emailInputName: PropTypes.string.isRequired,
  inputValue: PropTypes.string,
  onChange: PropTypes.func,
  inputWidth: PropTypes.string,
  hasError: PropTypes.bool,
  hasWarning: PropTypes.bool,
  placeholder: PropTypes.string,
  tabIndex: PropTypes.number,
  maxLength: PropTypes.number,
  className: PropTypes.string,

  isDisabled: PropTypes.bool,
  size: PropTypes.oneOf(['base', 'middle', 'big', 'huge']),
  scale: PropTypes.bool,

  clipActionResource: PropTypes.string,
  clipEmailResource: PropTypes.string,
  clipPasswordResource: PropTypes.string,

  tooltipPasswordTitle: PropTypes.string,
  tooltipPasswordLength: PropTypes.string,
  tooltipPasswordDigits: PropTypes.string,
  tooltipPasswordCapital: PropTypes.string,
  tooltipPasswordSpecial: PropTypes.string,

  generatorSpecial: PropTypes.string,
  passwordSettings: PropTypes.object.isRequired,

  onValidateInput: PropTypes.func,
  onCopyToClipboard: PropTypes.func,

  tooltipOffsetLeft: PropTypes.number
}

PasswordInput.defaultProps = {
  inputType: 'password',
  inputName: 'passwordInput',
  autoComplete: 'new-password',

  isDisabled: false,
  size: 'base',
  scale: true,

  clipEmailResource: 'E-mail ',
  clipPasswordResource: 'Password ',

  generatorSpecial: '!@#$%^&*',
  className: '',
  tooltipOffsetLeft: 110
}

export default PasswordInput;
/*
            {displayTooltip &&
              <DropDown directionY='top' manualY='150%' isOpen={true}>
                {tooltipContent}
              </DropDown>
            }
*/