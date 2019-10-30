import React from "react";
import PropTypes from "prop-types";
import Checkbox from "../../checkbox";
import Link from "../../link";

const ADSelectorRow = (props) => {
    const { label, isChecked, style, onChange, onSelect, isMultiSelect } = props;

    return (
        <div className="option" style={style}>
        {isMultiSelect ? (
          <Checkbox
            label={label}
            isChecked={isChecked}
            className="option_checkbox"
            onChange={onChange}
          />
        ) : (
          <Link
            as="span"
            truncate={true}
            className="option_link"
            onClick={onSelect}
          >
            {label}
          </Link>
        )}
      </div>
    );
};

ADSelectorRow.propTypes = {
    label: PropTypes.string,
    isChecked: PropTypes.bool,
    isMultiSelect: PropTypes.bool,
    style: PropTypes.object,
    onChange: PropTypes.func,
    onSelect: PropTypes.func
}

export default ADSelectorRow;