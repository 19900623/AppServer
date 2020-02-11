import React, { memo } from 'react'
import styled, { css } from 'styled-components'
import PropTypes from 'prop-types'
import CustomScrollbarsVirtualList from '../scrollbar/custom-scrollbars-virtual-list'
import DropDownItem from '../drop-down-item'
import Backdrop from '../backdrop'
import { FixedSizeList } from "react-window"
import onClickOutside from "react-onclickoutside";

const StyledDropdown = styled.div`
    font-family: 'Open Sans',sans-serif,Arial;
    font-style: normal;
    font-weight: 600;
    font-size: 13px;

    ${props => props.maxHeight && `
      max-height: ${props.maxHeight}px;
      overflow-y: auto;
    `}

    position: absolute;
    ${props => props.manualWidth && `width: ${props.manualWidth};`}
    ${props => (props.directionY === 'top' && css`bottom: ${props => props.manualY ? props.manualY : '100%'};`)}
    ${props => (props.directionY === 'bottom' && css`top: ${props => props.manualY ? props.manualY : '100%'};`)}
    ${props => (props.directionX === 'right' && css`right: ${props => props.manualX ? props.manualX : '0px'};`)}
    ${props => (props.directionX === 'left' && css`left: ${props => props.manualX ? props.manualX : '0px'};`)}
    z-index: 150;
    display: ${props => (props.open ? 'block' : 'none')};
    background: #FFFFFF;
    border-radius: 6px;
    -moz-border-radius: 6px;
    -webkit-border-radius: 6px;
    box-shadow: 0px 5px 20px rgba(0, 0, 0, 0.13);
    -moz-box-shadow: 0px 5px 20px rgba(0, 0, 0, 0.13);
    -webkit-box-shadow: 0px 5px 20px rgba(0, 0, 0, 0.13);

    padding: ${props => !props.maxHeight && props.children && props.children.length > 1 && `6px 0px`};
`;

// eslint-disable-next-line react/display-name, react/prop-types
const Row = memo(({ data, index, style }) => {
  const option = data[index];

  return (
    <DropDownItem
      // eslint-disable-next-line react/prop-types
      {...option.props}
      style={style} />
  );
});

class DropDown extends React.PureComponent {
  constructor(props) {
    super(props);

    this.state = {
      width: this.dropDownRef ? this.dropDownRef.current.offsetWidth : 240,
      directionX: props.directionX,
      directionY: props.directionY
    };

    this.dropDownRef = React.createRef();
  }

  componentDidMount() {
    if (this.props.open) {
      this.props.enableOnClickOutside();
      this.checkPosition();
    }
  }

  componentWillUnmount() {
    this.props.disableOnClickOutside();
  }

  componentDidUpdate(prevProps) {
    if (this.props.open !== prevProps.open) {
      if (this.props.open) {
        this.props.enableOnClickOutside();
        this.checkPosition();
      }
      else {
        this.props.disableOnClickOutside();
      }
    }
  }

  handleClickOutside = e => {
    this.toggleDropDown(e);
  };

  toggleDropDown = (e) => {
    this.props.clickOutsideAction && this.props.clickOutsideAction(e, !this.props.open);
  }

  checkPosition = () => {
    if (!this.dropDownRef.current) return;

    const rects = this.dropDownRef.current.getBoundingClientRect();
    const container = { width: window.innerWidth, height: window.innerHeight };
    const left = rects.left < 0 && rects.width < container.width;
    const right = rects.left < 250 && rects.left > rects.width && rects.width < container.width;
    const x = left ? 'left' : right ? 'right' : this.props.directionX;

    this.setState({
      directionX: x,
      width: rects.width
    });
  }

  render() {
    const { maxHeight, children } = this.props;
    const { directionX, directionY, width } = this.state;
    const isTablet = window.innerWidth < 1024; //TODO: Make some better
    const itemHeight = isTablet ? 36 : 32;
    const fullHeight = children && children.length * itemHeight;
    const calculatedHeight = ((fullHeight > 0) && (fullHeight < maxHeight)) ? fullHeight : maxHeight;
    const dropDownMaxHeightProp = maxHeight ? { height: calculatedHeight + 'px' } : {};
    //console.log("DropDown render");
    return (
      <StyledDropdown
        ref={this.dropDownRef}
        {...this.props}
        directionX={directionX}
        directionY={directionY}
        {...dropDownMaxHeightProp}
      >
        {maxHeight
          ? <FixedSizeList
            height={calculatedHeight}
            width={width}
            itemSize={itemHeight}
            itemCount={children.length}
            itemData={children}
            outerElementType={CustomScrollbarsVirtualList}
          >
            {Row}
          </FixedSizeList>
          : children}
      </StyledDropdown>
    );
  }
}

DropDown.propTypes = {
  children: PropTypes.any,
  className: PropTypes.string,
  clickOutsideAction: PropTypes.func,
  directionX: PropTypes.oneOf(['left', 'right']),
  directionY: PropTypes.oneOf(['bottom', 'top']),
  disableOnClickOutside: PropTypes.func,
  enableOnClickOutside: PropTypes.func,
  id: PropTypes.string,
  manualWidth: PropTypes.string,
  manualX: PropTypes.string,
  manualY: PropTypes.string,
  maxHeight: PropTypes.number,
  open: PropTypes.bool,
  style: PropTypes.oneOfType([PropTypes.object, PropTypes.array]),
  withBackdrop: PropTypes.bool
};

DropDown.defaultProps = {
  directionX: 'left',
  directionY: 'bottom',
  withBackdrop: false
};

const EnhancedComponent = onClickOutside(DropDown);

class DropDownContainer extends React.Component {
  render() {
    const { withBackdrop = false, open } = this.props;
    const isTablet = window.innerWidth < 1024; //TODO: Make some better
    return (
      <>
        <EnhancedComponent disableOnClickOutside={true} {...this.props} />
        {(withBackdrop && open && isTablet) && <Backdrop visible zIndex={149} onClick={this.toggleDropDown} />}
      </>);
  }
}

DropDownContainer.propTypes = {
  open: PropTypes.bool,
  withBackdrop: PropTypes.bool
}

export default DropDownContainer;
