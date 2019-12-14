import Checkbox from "../checkbox";
import ContextMenuButton from "../context-menu-button";
import PropTypes from "prop-types";
import React from "react";
import isEqual from "lodash/isEqual";
import styled from "styled-components";
import { tablet } from "../../utils/device";

const StyledRow = styled.div`
  cursor: default;

  min-height: 50px;
  width: 100%;
  border-bottom: 1px solid #eceef1;

  display: flex;
  flex-direction: row;
  flex-wrap: nowrap;

  justify-content: flex-start;
  align-items: center;
  align-content: center;
`;

const StyledContent = styled.div`
  display: flex;
  flex-basis: 100%;

  min-width: 160px;

  @media ${tablet} {
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }
`;

const StyledCheckbox = styled.div`
  flex: 0 0 16px;
`;

const StyledElement = styled.div`
  flex: 0 0 auto;
  display: flex;
  margin-right: 8px;
  margin-left: 2px;
  user-select: none;
`;

const StyledOptionButton = styled.div`
  display: flex;

  .expandButton > div:first-child {
    padding-top: 8px;
    padding-bottom: 8px;
    padding-left: 16px;
  }
`;

class Row extends React.Component {
  shouldComponentUpdate(nextProps) {
    if (this.props.needForUpdate) {
      return this.props.needForUpdate(this.props, nextProps);
    }
    return !isEqual(this.props, nextProps);
  }

  render() {
    //console.log("Row render");
    const {
      checked,
      element,
      children,
      data,
      contextOptions,
      onSelect
    } = this.props;

    const renderCheckbox = Object.prototype.hasOwnProperty.call(
      this.props,
      "checked"
    );

    const renderElement = Object.prototype.hasOwnProperty.call(
      this.props,
      "element"
    );

    const renderContext =
      Object.prototype.hasOwnProperty.call(this.props, "contextOptions") &&
      contextOptions.length > 0;

    const changeCheckbox = e => {
      onSelect && onSelect(e.target.checked, data);
    };

    const getOptions = () => contextOptions;

    return (
      <StyledRow {...this.props}>
        {renderCheckbox && (
          <StyledCheckbox>
            <Checkbox isChecked={checked} onChange={changeCheckbox} />
          </StyledCheckbox>
        )}
        {renderElement && <StyledElement>{element}</StyledElement>}
        <StyledContent>{children}</StyledContent>
        <StyledOptionButton>
          {renderContext && (
            <ContextMenuButton className="expandButton" directionX="right" getData={getOptions} />
          )}
        </StyledOptionButton>
      </StyledRow>
    );
  }
}

Row.propTypes = {
  checked: PropTypes.bool,
  children: PropTypes.element,
  className: PropTypes.string,
  contextOptions: PropTypes.array,
  data: PropTypes.object,
  element: PropTypes.element,
  id: PropTypes.string,
  needForUpdate: PropTypes.func,
  onSelect: PropTypes.func,
  style: PropTypes.oneOfType([PropTypes.object, PropTypes.array])
};

export default Row;
